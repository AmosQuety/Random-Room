# Build list: what to build next, and how

This file lists only work that is still to do. Each item says why it matters, where it touches the code, the steps to
build it, how to test it, and what is still undecided, so you can start without asking anyone. The reasons behind past
choices are in `DECISIONS.md`; known gaps are in `Gaps_Bugs.md`; the research this list draws on is in
`docs/research/game-platform-report.md` (with per-repository notes in `docs/research/notes/`).

## Order and dependencies

| # | Item | Size | Needs first | Owner decision? |
|---|---|---|---|---|
| 1 | Serve the game facts from the API instead of keeping a frontend copy (the contract tests that guard the copy are done) | S | none | **yes: wanted at all?** |
| 3 | Referee host, phase 1: the host does not play by default | L | none (the contract tests are in) | confirm the design below |
| 4 | Referee host, phase 2: the host can choose to play | M | 3 | none |
| 5 | Data model: a game session owns its game type and setup | M-L | none (the bot tests are in) | **go-ahead (migration on live data)** |
| 6 | Host "Change game" between games, including the "play again or choose another game" choice on Start new game | M | 5 | who may choose |
| 7 | Add a player to a room | S-M | none (more useful after 6) | wanted at all? |
| 8 | "Tonight's scoreboard" across games | M | 5 | scoring rule, reset scope |
| 9 | Next-game vote | M | 6 | host override, repeats |
| 10 | Host hand-over | S | none | manual or automatic |
| 11 | New-version banner | S | none | no |
| 12 | Host status view | S | none | no |
| 13 | Reuse a past setup (saved in the browser) | S | none | no |
| 14 | Spectator link (only if asked for) | M | none | wanted at all? |
| 15 | Before real use: content review, database, deploy settings, load test, real devices | tasks | none | several |

Recommended order: 3 and 4 (the referee model changes how every game counts players, so do it before the
multi-game work), then 5, 6, 7, 8, 9. Item 1 is optional and Items 10 to 13 can go in any gap.

## Ground rules for every item

- **Tests:** write the code, then tests that confirm it does what was intended (this project does not use test-first).
  Prove each new test can fail: temporarily remove the new code and watch it go red. Keep one assertion idea per test.
- **Run before you commit:** `docker start randomroom-pg` (Postgres on port 5433), then
  `dotnet test backend/RandomRoom.slnx` (about 1.5 minutes), and in `frontend/`: `npx vitest run`, `npx tsc -b`,
  `npm run lint` (one old warning in `useRoom.ts` is expected), `npm run check:contrast`, `npm run build`.
- **Database changes** are additive migrations: from `backend/RandomRoom.Api` run
  `export DOTNET_ROOT=/usr/share/dotnet; dotnet ef migrations add <Name> -o Data/Migrations`. Test against a database
  that already has data. Never edit an old migration.
- **API tests and rate limits:** join and recover share 20 requests a minute per address, create-room and claim 40. A test
  class that signs many players in needs its own server (`IClassFixture<ApiTests.Factory>`), like `SeatResetTests`.
- **Docs:** add the reasoning to `DECISIONS.md`, update `README.md` if behaviour a user sees changes, and remove the
  item from this file when it is done.
- **Commits:** small, one idea each, conventional style (`feat(api): ...`). No AI attribution anywhere. Do not push to
  `main`; use a branch and a pull request.
- **Security:** the server decides everything. Hiding a button is not authorisation. Never put hidden information in a
  payload for someone not entitled to it. Audit rows go in `RoomAuditEvents` (append-only).

## Map of the code you will touch

- **Server (`backend/RandomRoom.Api`)**
  - `Games/IGameEngine.cs`: what a game must provide (`GameType`, `MinPlayers`, `MaxPlayers`, `ConfigureRoomAsync`,
    `OnSessionCreatedAsync`, `HandleActionAsync`, `GetPayloadAsync`, `GetPayloadForAsync` (per player), completion).
  - `Games/Shared/GameStore.cs`: setup and per-session state helpers. `PlayerNamesAsync(roomId)` (all seats, ordered by
    created time then name) and `HostOfAsync`. **Three engine spots count players directly from `RoomPlayers`:**
    `RandomPickerEngine.cs:89`, `TriviaEngine.cs:218` and `:258`.
  - `Services/GameSessionService.cs`: start, end, new session, snapshots (always built in `BuildSnapshotAsync`, untracked).
  - `Services/RoomAdminService.cs`: create room, preview, claim, reset seat, recovery code, delete room.
  - `Services/RoomSnapshotSequencer.cs`, `RoomNotifier.cs` (SignalR pushes), `RoomRetentionService.cs`.
  - `Domain/`: `Room` (`GameType`, `HostPlayer`, `RecoveryCodeHash`), `RoomPlayer`, `GameSession`, `RoomGameSetup`
    (one row per room today), `GameSessionState`, `GameEntry`, `RoomAuditEvent`.
  - `Program.cs`: registers every engine; migration policy in `Data/DatabaseMigrationPolicy.cs`.
- **Frontend (`frontend/src`)**
  - `games/types.ts` (`GameModule`, `GameScreenProps`: `me`, `isHost`, `players`, `session`, `payload`, `busy`,
    `onAction`), `games/registry.ts` (the list of games), one folder per game.
  - `components/RoomScreen.tsx` (picks the game screen from the snapshot), `CreateRoomScreen.tsx` (the create flow),
    `HostControls.tsx`, `SeatControls.tsx`, `DeleteRoomControl.tsx`, `lib/useRoom.ts` (snapshot plus SignalR).
- **Tests:** `backend/RandomRoom.Tests` (xUnit, real Postgres; `GameHarness` for one game, `RoomTestHarness`,
  `ApiTests.Factory` for HTTP) and `frontend/src/**/*.test.tsx` (Vitest).

---

## 1. Serve the game facts from the API (S, optional)

**Done already:** contract tests keep the frontend's copy of the server's facts honest (game keys, player ranges, trivia
limits, starter-pack sizes). Backend `ServerFactsTests` writes what the engines say to
`frontend/src/games/__fixtures__/server-facts.json` and fails if the committed file is out of date; the frontend's
`games/serverFacts.test.ts` compares its own copies with that file and fails, never skips, when it cannot compare. To
refresh the fixture after a deliberate change: `UPDATE_SERVER_FACTS=1 dotnet test backend/RandomRoom.slnx --filter ServerFactsTests`.
The reasoning is in `DECISIONS.md` ("Server facts the frontend copies").

**What is left, and needs a decision.** `GET /api/games` returning those facts, with the frontend reading them instead of
keeping its own copy. That removes the duplication instead of guarding it, but it adds a request before the picker can
draw and a loading and error state to the create flow, which matters on slow connections. Only worth it if the guarded
copy keeps causing friction. Item 3 and later add more per-game facts (for example whether a game can run with a referee),
which is the moment to reconsider.

## 3. Referee host, phase 1: the host does not play by default (L) - frontend still to do

**Backend done** (see `DECISIONS.md`, "Referee host (phase 1, backend)"): `Room.HostPlays` (migration, existing rooms
`true`), optional `hostPlays` on create (default `true`), participants instead of seats in every engine, ranges checked
against participants (seat cap 12), `RoomSnapshot.HostPlays` and `PlayerView.Plays`, the central referee check
(`IGameEngine.RefereeActions`, 403 "You are refereeing this game"), Forbidden Words card and flag for a referee, and the
backend tests (`RefereeHostTests`, `RefereeApiTests`).

**Still to do (frontend):**
1. `lib/types.ts`: `RoomSnapshot.hostPlays`, `PlayerView.plays`. `lib/players.ts` `playerCountIssue` takes the participant
   count; Buzzer needs only 2 participants with a referee (`MinPlayersWithReferee`), which the server-facts fixture does
   not carry yet (`ServerFactsTests` and the fixture need that fact added).
2. `GameScreenProps` gets `referee: boolean` (`isHost && !hostPlays`). Every game screen: hide answer/vote/buzz/mark/draw
   controls, show a short "You are refereeing" line, keep the host controls, keep the scoreboard to participants. Batches:
   the round games first; then Wheel, Bingo, Buzzer, story games, Two Truths, Guess Who, Word Spies, Forbidden Words
   (show the card for `role: "referee"` and a flag button), Sketch Guess, Random Picker.
3. `CreateRoomScreen.tsx`: send `hostPlays: false`; the Players step explains "You are the host and referee. Add the people
   who will play." and shows the participant count against the game's range. The host seat is still entered first.
4. Update `README.md` ("the host runs the game and, by default, does not play") once the UI ships.

**Tests still to do:** frontend, for each screen, render as referee and assert no play controls and the host controls
present. Optional backend: a referee bot in `backend/RandomRoom.Tests/EndToEnd`.
Also from the independent review (low severity): nothing proves a referee can still *run* every game, so a typo in an
engine's `RefereeActions` would lock the referee out without a failing test. Add one test per engine in which the referee
sends each allowed control action, and assert each set only names actions that engine handles. Open question for the
owner: in a Buzzer room where the host plays, the snapshot says `Plays=true` for a host who never buzzes; intended?

**Watch out.** Player-count rules show up in three places that must agree (server validation, `playerCountIssue`, the
engine `MinPlayers`/`MinPlayersWithReferee`); the server-facts contract tests help.

## 4. Referee host, phase 2: the host can choose to play (M)

**Why.** Some groups want the host in the game. Keep the fair-play rules from item 3's "already built" list in force
whenever the host plays.

**Build.**
1. Endpoint `POST /api/room/host-plays` with `{ "plays": true|false }`: host only, refused while a game is active
   (`409`), audited (`host-plays-changed`), and broadcast so every screen refreshes. Takes effect from the next game.
2. Host controls: a toggle "Play in the next game" in `HostControls` (or beside the seat panel), with a one-line
   explanation of what changes (you appear on the scoreboard; another player gives you points in Spin the Wheel; you can
   no longer flag in Forbidden Words unless you are the judge).
3. Games that need a referee ignore the toggle (Buzzer: the host always judges): show it disabled with the reason.
4. Participant-count check on the toggle: if turning it on pushes a game over its `MaxPlayers`, refuse with a clear message.
5. Creation flow: add "I want to play too" (off by default) on the Players step, sending `hostPlays`.

**Tests.** Toggle refused mid-game; audit row written; with the host playing, every safeguard holds (wheel: host cannot
award themselves; Forbidden: host cannot flag unless judge; notes listed); Buzzer ignores the toggle.

## 5. Data model: a game session owns its game type and setup (M-L)

**Why.** A room is locked to one game for its whole life, so changing games means a new room, new invites and new PINs.
Players get bored of one game; the owner wants to change games in the same room. The research found one working pattern in
the studied ecosystem: the people layer outlives the game, and only the game layer is replaced. That means moving game type
and setup from the room to the session. Full reasoning and the alternative (linking rooms) are in
`docs/plan-switch-games-in-a-room.md`; the research report confirms option A. **This is the risky step: a migration on live
data. It ships alone, with no behaviour change.**

**Today.** `Room.GameType` is fixed. Setup lives in `RoomGameSetups` (unique on `RoomId`), `RoomChoices` (Random Picker) and
`TriviaQuestions` (Trivia), all keyed by `RoomId`. Every engine reads setup with `GameStore.GetSetupAsync(roomId)` or its own
room-keyed table. `IGameEngine.ConfigureRoomAsync(roomId, setup)` runs once at room creation.

**Build.**
1. Migration: `GameSession.GameType` (string; backfill from the room); `RoomGameSetup.SessionId` and drop the unique index
   on `RoomId` (new unique index on `SessionId`); `RoomChoice.SessionId`; `TriviaQuestion.SessionId`. Backfill by copying each
   room's rows to each of its sessions so nothing in flight changes. Keep it reversible.
2. `Room.GameType` becomes "the current game" (the latest session's); the join page and room preview read the latest session.
3. Change `IGameEngine.ConfigureRoomAsync(roomId, setup)` to configure a **session**, and change all 19 engines from
   `GetSetupAsync(roomId)` to `GetSetupAsync(sessionId)`. It is mechanical; the existing per-game tests cover it once updated.
4. "Start new game" of the **same** game copies the previous session's setup, so behaviour is exactly as today.
5. `GameSessionService.StartNewSessionAsync` (and room creation) create the session with its game type and setup.
6. Frontend: `RoomScreen` reads the game from the current session (`snapshot.session.gameType`), not the room.

**Tests.** The whole existing suite unchanged and green; a migration test run against a copy of realistic data (rooms with
several sessions, each game type); run the end-to-end bots across a "Start new game" to prove nothing changed; a concurrency
test: end game at the same instant as an action still ends cleanly.

**Needs from the owner:** go-ahead for option A. Fallback if it proves too large: linked rooms (plan document).

## 6. Host "Change game" between games (M)

**Why.** The feature the owner wants: pick a different game in the same room, same invites, same PINs.

**Build.**
1. Extend `POST /api/room/session/new` with optional `gameType` and `setup` (absent means same game, same setup, as today).
   Host only; refused while a game is running; refused if the participant count (item 3) does not fit the game's range or
   the setup is invalid; audited (`game-changed`); the push after it carries the new game type.
2. Extract the create flow's **setup step** into a reusable component and use it for "Change game" in the host controls.
   The game picker lists only games that fit the room's participant count and says why the others do not.
3. Hidden information: nothing from the old game may appear in the new payload (payloads are per session; assert it).
4. Games that need more players than the room has cannot be picked until item 7 exists.
5. **What the host sees when they press "Start new game" (requested by the owner).** Today the button
   (`components/HostControls.tsx:37`, wired in `RoomScreen.tsx:252` to `startNewSession` in `lib/api.ts:70`) immediately
   creates the next session and **starts it** (`GameSessionService.StartNewSessionAsync`, which activates the new session
   in one step). Replace that with a choice, shown as a popup or an inline panel:
   - **"Play <current game> again"**: the same game with the same setup, exactly what the button does now (call
     `session/new` with no body). This should be the default and the first button, so the common case stays one tap.
   - **"Choose a different game"**: shows the game picker (item 6.2), then that game's setup step, then calls
     `session/new` with `gameType` and `setup`. Only games that fit the room's participant count are selectable; the
     others show why (for example "Needs 4 players, you have 3").
   - **Cancel** closes it and changes nothing.
   - A different game with a setup should not start on the instant the setup is submitted by accident: show a final
     "Start <game>" button on the setup step, then start.
   - If the host-plays toggle exists (item 4), offer "Play in this game" in the same panel, since that choice also takes
     effect from the next game.
   - **Presentation:** on a phone a bottom sheet or an inline panel that expands under the button is easier than a
     centred popup (reachable with a thumb, no keyboard fight). If you use a modal dialog it needs a proper focus trap,
     Escape to close, focus returned to the button on close, and `aria-modal`. The existing `ConfirmButton` is inline on
     purpose (no focus trap, light on slow phones); keep that spirit unless a modal is clearly better.
   - Until item 5 (data model) exists, only the first option can work; ship the panel with just "Play again" hidden
     behind the same call, so nothing changes for users, then add the second option when the server supports it.

**Tests for the choice:** pressing "Start new game" shows both options and starts nothing; "Play again" calls the old
request with no body; "Choose a different game" lists only games that fit and shows the reason for the others; cancelling
changes nothing; Escape and focus return work if it is a dialog; a non-host never sees it.

**Tests (including the case the research found missing in the studied ecosystem):** switch A to B to A with bots online;
**a player joining or reconnecting at the instant of the switch**; a Word Spies key and a Bingo card absent after a
switch; switching refused mid-game; audit row.

## 7. Add a player to a room (S-M)

**Why.** Seats are fixed at creation, so a game that needs more players (Word Spies needs 4) cannot be offered to a small
room, and a forgotten friend means a new room. Item 6's game list depends on it.

**Build.** `POST /api/room/players` (host only, between games): name rules as at creation (unique, at most 32 characters),
seat cap 12, new invite token returned once, audit row `seat-added`, snapshot push. Frontend: "Add a player" in the seat
panel (`SeatControls`), then the same link actions (Share, Copy, QR) as everywhere. Refuse during a game.

**Tests.** Duplicate name, cap, mid-game refusal, invite claim works, audit row, the new player appears for everyone.

## 8. "Tonight's scoreboard" across games (M)

**Why.** Scores are per game today. A room that plays several games wants one running board. The studied ecosystem **never**
adds scores across different games (they are not comparable); it keeps boards per mode. So combine **placings**, not points.

**Build.**
1. Table `SessionResult` (`SessionId`, `Player`, `Placing`, `Points?`), written when a session completes (the callers around
   `GameSessionService` completion). Each engine exposes a final placing per participant (ties share a placing; use the shared
   ranking rule in `frontend/src/lib/ranking.ts` as the reference). Games with no winner (Random Picker, story games) give
   no placings and do not count.
2. A room-wide board: sum of placing points (for example 1st = participants, 2nd = participants-1, ...), shown in the room.
3. Reset scope decision: per room for ever, or a "new evening" button that starts a new board.

**Tests.** Ties, early-ended games, games without winners, a referee never appears, a player added later.

**Needs from the owner:** the scoring rule and reset scope.

## 9. Next-game vote (M)

**Why.** After a game, let the room choose what is next. The studied ecosystem builds the ballot from the same list that
launches games, filters by player count, keeps the home screen off it, refuses loudly if the ballot would be empty, and
needs no more than a handful of rules for a friends' room (it has 80 settings and no veto; do not copy that).

**Build.**
1. Host opens a ballot of 3 to 5 games (the room's recent or favourite games, filtered by participant count). The ballot is
   part of the snapshot, so a late joiner sees it.
2. Each participant votes once; plurality wins; the vote closes when everyone has voted or the host closes it; ties go to
   the host's choice or a fixed order, never display order; the result records how it was decided.
3. The result calls item 6's "Change game". One deadline owner (`ServerTimer`), no background clocks. If the ballot cannot
   open (too few games fit), say so and fall back to the host choosing.
4. Rules to decide: may the host override the result; may the same game repeat straight away (suggested cooldown: one play).

**Tests.** All voted, partial votes at the deadline, tie, late joiner sees the ballot, a ballot with too few games, a vote
recorded once (a double click is harmless).

## 10. Host hand-over (S)

**Build.** Host-only "Make X the host" between games: changes `Room.HostPlayer`, **re-issues the recovery code to the new
host and revokes the old one**, signs out nothing, audits (`host-changed`). Frontend: in the seat panel. Decide whether an
automatic hand-over (host offline for N minutes) is wanted; start with manual only. In referee mode the old host becomes a
normal participant.

**Tests.** Old recovery code stops working, new host can do host actions, old host cannot, a non-host cannot, audit row.

## 11. New-version banner (S)

**Build.** Put a build id in each snapshot (`Services/RoomSnapshot.cs`; read it once from the assembly version or a
configured value). In `lib/useRoom.ts`, remember the first id seen; if a later snapshot has a different one, show a
"A new version is available, refresh" banner. **Tests:** same id shows nothing; a different id shows the banner.

## 12. Host status view (S)

**Build.** In the room, a host panel that says what the room is doing: current game and phase, who is online, who has not
opened their invite link, and the deadline if one is running. Mostly existing data (`PlayerView.online`, `claimed`, the
timer view); add a small section in `RoomScreen` for the host. This also delivers the old "host lobby view"
(Gaps_Bugs G1 item 4). **Tests:** renders each state; shows nothing sensitive.

## 13. Reuse a past setup (S)

**Build.** In the create flow and in "Change game" (item 6), save the last setup per game in `localStorage` (try/catch,
works without it) and offer "Use my last [game] setup". Never store anything sensitive: setups hold prompts and lists only.
**Tests:** saves and restores; ignores a corrupt entry; blocked storage does not break the flow.

## 14. Spectator link (only if asked for) (M)

**Why.** We already build a viewer-less public view of every game, so a read-only watcher is mostly access control.

**Build.** A host-created spectator link (a secret token, revocable, audited) that joins as a viewer who receives only the
public payload and never counts toward votes or player minimums. **Risk:** the link is a shared secret; decide whether
anyone holding it can watch or only people the host admits. **Tests:** a spectator never receives a secret (reuse the end-to-end bots'
secret scan); revoking the link disconnects them.

## 15. Before real use: tasks that are not code

1. **Review the East Africa trivia pack.** It is wired in (Trivia setup > Question set > East Africa, 20 questions) but
   nobody has checked the questions. Play it, check every answer, swap in questions your players know. Source:
   `backend/RandomRoom.Api/Games/Content/trivia-east-africa.json`; notes in `docs/trivia-east-africa-draft.md`. If you
   change the count, update `STARTER_PACKS` in `frontend/src/games/trivia/setup.ts` too (the server-facts contract test checks this).
2. **Choose the database host.** Suggested: Render Postgres on a paid plan if the API runs on Render (same region, internal
   URL, backups), or Neon's free tier if cost matters most (it sleeps when idle). Put the API and database in the same
   region. Check current prices first. Keep the host's backup retention no longer than `Room__RetentionDays`.
3. **Deploy settings:** set `Database__AutoMigrate=true` in production (the app refuses to start on a database that needs
   migrations otherwise); leave it unset locally. `Room__RetentionDays` stays 30 unless the owner decides otherwise. TLS is
   required automatically for any database that is not on the same machine: test the first connection, and if a host
   refuses, see the README section "Database encryption" (`?sslmode=prefer` or `Trust Server Certificate=true`). Set
   `Room__JwtSigningKey` (32+ random characters).
4. **Load test** realistic concurrent rooms (for example 50 rooms of 8 players) and size the connection pool deliberately.
   Nothing has measured this yet.
5. **Test on real phones and with a screen reader** (everything so far used emulation). Check the QR code scan on a real
   phone, the share sheet, and the keyboard covering inputs.
