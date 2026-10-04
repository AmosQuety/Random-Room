# Plan: switch games inside one room

Status: **plan only, nothing built.** Written 2026-10-03. Motivation, from the owner: players (and the owner as a
gamer) get bored playing one game for a whole evening and want to change to another without starting over.

## The problem today

A room is fixed to one game for its whole life (`Room.GameType`). To play something else, the host makes a new room,
sends every player a new invite, and every player chooses a new PIN. The friction is the repeated sign-in as much as the
game lock.

## What is in the way (checked against the code)

- **Game type lives on the room**, not on a game. `GameSession` has no game type.
- **Setup and game content are keyed by room**, not by session: `RoomGameSetups` (one row per room, unique index on
  `RoomId`), `RoomChoices` (Random Picker) and `TriviaQuestions` (Trivia) all hang off `RoomId`. Every engine reads its
  setup through `GameStore.GetSetupAsync(roomId)` or its own room-keyed table.
- **`IGameEngine.ConfigureRoomAsync(roomId, setup)`** runs once, when the room is created.
- **Seats are fixed at creation.** A game that needs more players than the room has cannot be switched to, and there is no
  way to add a player to a room later.
- **The frontend picks the game screen from the room** (`GAMES[snapshot.gameType]`), and the room's accent colour and
  title bar follow it.
- **Scores are per game.** Each engine has its own scoreboard shape; there is no room-wide result.

## Two ways to do it

**A. A room can change its game between games (the target).** `GameSession` gets a `GameType`, setup moves from the room
to the session, and the host can pick a different game (with its own setup) when no game is running. Same room, same
invites and PINs, same scoreboard. This is the proper fix and the larger change.

**B. Linked rooms (the cheap alternative).** "Play something else" creates a new room with the same players, copies each
seat's PIN hash, and lets a player move across with their old token (one call, authenticated by the old room). No change
to engines or to how setup is stored, and retention works unchanged. But the players still change page and URL for every
game, and a "tonight's scoreboard" is harder to build across a chain of rooms.

Recommendation: plan for **A**, but decide when we start. If the data-model change looks too big once the contract tests
(below) are in, **B** gets the main benefit (no new invites, no new PINs) at a fraction of the cost.

## What A needs

**Data model (one migration, mostly mechanical)**
1. `GameSession.GameType` (string). Backfill every existing session from its room.
2. Setup keyed by session: `RoomGameSetup` gets `SessionId` (replace the room unique index); `RoomChoices` and
   `TriviaQuestions` the same. Backfill: copy each room's rows to each of its sessions so nothing already in flight
   changes. Starting a new game of the **same** type copies the previous session's setup, so "Start new game" behaves
   exactly as it does now.
3. `Room.GameType` becomes "the current game" (or is dropped in favour of the latest session's), with the join page and
   room preview reading the latest session.

**Engines.** `ConfigureRoomAsync(roomId, ...)` becomes "configure this session". All 19 engines change from
`GetSetupAsync(roomId)` to `GetSetupAsync(sessionId)`; it is a mechanical edit, covered by the existing per-game tests
once they are updated.

**API.** Host-only `POST /api/room/session/new` takes an optional `gameType` and `setup` (absent means "same game, same
setup", as today). Refused while a game is running (the host ends it first, as with seat resets), when the room has too
few or too many players for the game, or when the setup is invalid. Recorded in the audit table. The push that follows
carries the new game type, so every player's screen switches.

**Frontend.** The setup step of the create flow becomes a reusable component, used by "Change game" in the host controls.
`RoomScreen` takes the game from the current session. The room accent and header follow the game.

**Player count.** Switching to a game the room has too few players for needs "add a player to this room" (host adds a
seat and gets a new invite). That is its own small feature and a prerequisite for games like Word Spies (4+) in a room of 3.
Until then the "Change game" list shows only games that fit, and says why the others do not.

**Hidden information.** No new risk: every payload is already keyed by session and built per viewer, so one game's
secrets cannot appear in the next game's payload. Tests should assert it anyway (a spymaster key, a Bingo card, the
drawer's word after a switch).

## Later phases

- **Tonight's scoreboard.** At completion, store each session's result (placing per player) in a new `SessionResult`
  table, and show a room-wide board. Decision needed: how to compare games that score differently (raw points do not
  compare; placings do).
- **Next-game vote.** After a game ends, the host opens a ballot of 3 to 5 games (or the room's favourites), players
  vote, the host confirms or it starts automatically. Reuses the same switch call. Rules to decide: host veto, ties,
  whether a game can repeat straight away.
- **Playlist.** The host queues several games at the start of the night.

## Order of work

0. **Prerequisite: contract tests** that fail when frontend and server copies of the same number differ (trivia caps,
   pack sizes, player ranges). A change this size touches all of them. (Item 1 in NEXT_STEPS.)
1. **Data model only, no behaviour change.** Add the columns, key setup by session, update engines and tests. Everything
   still works exactly as before. This is the risky step, so it ships alone.
2. **Host "Change game" between games**, with the player-count check and the audit record.
3. **Add a player to a room** (so any game can be switched to).
4. **Tonight's scoreboard.**
5. **Next-game vote.**

## Risks

- A migration that moves setup keys on data that matters. Test it against a representative database first, and keep it
  reversible.
- A host switching at the same moment players act. The existing session-state and snapshot-sequence rules should cover it
  (this is the End-game-versus-answer race again), but it needs its own concurrency test.
- Retention: a long-lived room now accumulates several games' content. The retention job already deletes whole rooms, so
  nothing new, but the size per room grows.
- Scope creep into "game rooms as a platform". Keep each step shippable on its own.

## Open questions for the owner

1. Is the switch always the host's choice, or should players be able to propose or vote from the start?
2. Should "tonight's scoreboard" reset per room, per evening, or never?
3. Is adding players to a room mid-evening wanted, or is the seat list meant to stay fixed?
4. If A proves too large, is B (linked rooms) acceptable?

## What this came from

Reading the `game-playground` repo (Godot 4, MIT licence): its map rotation, ballot and game descriptor suggested the
shape. That repo rotates maps inside one game, not different games, so the evidence for the feature is the owner's own
experience rather than that repo.
