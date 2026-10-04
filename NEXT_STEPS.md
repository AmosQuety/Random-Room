# Build list: what to build next, and how

This file lists only work that is still to do. Each item says why it matters, where it touches the code, the steps to
build it, how to test it, and what is still undecided, so you can start without asking anyone. The reasons behind past
choices are in `DECISIONS.md`; known gaps are in `Gaps_Bugs.md`; the research this list draws on is in
`docs/research/game-platform-report.md` (on the branch `research/game-platform-report`, merge it to read it in `main`).

## Order and dependencies

| # | Item | Size | Needs first | Owner decision? |
|---|---|---|---|---|
| 1 | Contract tests: server and frontend facts cannot drift | S | none | only for the optional endpoint |
| 2 | Bot end-to-end tests over the real API | M | none | run on every PR, or nightly |
| 3 | Referee host, phase 1: the host does not play by default | L | 1 | confirm the design below |
| 4 | Referee host, phase 2: the host can choose to play | M | 3 | none |
| 5 | Data model: a game session owns its game type and setup | M-L | 1, 2 | **go-ahead (migration on live data)** |
| 6 | Host "Change game" between games | M | 5 | who may choose |
| 7 | Add a player to a room | S-M | none (more useful after 6) | wanted at all? |
| 8 | "Tonight's scoreboard" across games | M | 5 | scoring rule, reset scope |
| 9 | Next-game vote | M | 6 | host override, repeats |
| 10 | Host hand-over | S | none | manual or automatic |
| 11 | New-version banner | S | none | no |
| 12 | Host status view | S | none | no |
| 13 | Reuse a past setup (saved in the browser) | S | none | no |
| 14 | Spectator link (only if asked for) | M | none | wanted at all? |
| 15 | Before real use: content review, database, deploy settings, load test, real devices | tasks | none | several |

Recommended order: 1, 2, then 3 and 4 (the referee model changes how every game counts players, so do it before the
multi-game work), then 5, 6, 7, 8, 9. Items 10 to 13 can go in any gap.

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

## 1. Contract tests: server and frontend facts cannot drift (S)

**Why.** The same facts are written twice, once on the server and once in the frontend: the list of game keys, each
game's player range, the trivia limits (6 options, 100 characters per option, 300 per question, 50 questions) and the
starter-pack sizes. A change to one copy that misses the other has caused bugs before (accent-duplicate checks, three
times). The research found the same disease in the studied ecosystem, and also that their own comparison test silently
passed when it could not compare. Ours must fail instead.

**Where.** Server: `Program.cs` (registered engines), each engine's `GameType`/`MinPlayers`/`MaxPlayers`,
`Games/Trivia/TriviaEngine.cs` (limits, `StarterPacks`). Frontend: `games/registry.ts` (`GAME_LIST`, each module's `key`,
`minPlayers`, `maxPlayers`), `games/trivia/setup.ts` (limits, `STARTER_PACKS`).

**Build.**
1. Backend test that builds the service provider the way `Program.cs` does (or lists `IGameEngine` registrations via
   `WebApplicationFactory`), and writes a JSON file of `{ key, minPlayers, maxPlayers }` for every engine, plus the trivia
   limits and pack sizes read from `TriviaEngine`/`ContentBank`. Write it to a path the frontend test can read
   (for example `frontend/src/games/__fixtures__/server-facts.json`, committed).
2. Frontend Vitest test that loads the fixture and compares it with `GAME_LIST`, `trivia/setup.ts` and
   `STARTER_PACKS`: same keys (no extras either way), same ranges, same limits, same pack sizes.
3. **The test must fail when it cannot compare:** missing fixture, empty fixture, or a game present on one side only.
   Never skip, never pass an empty comparison.
4. A backend check that the committed fixture matches what the engines say now (regenerate-and-diff), so an engine change
   makes the build go red until the fixture is updated.
5. Fix the stale doc comment in `Services/RoomSnapshot.cs` (it says one view is sent to every participant; it is per
   viewer now).
6. Optional, needs a decision: `GET /api/games` returning those facts, with the frontend reading it instead of keeping
   its own copy. Do this only after the tests exist.

**Test the test.** Change `maxPlayers` on one side and see red; delete the fixture and see red; add a game on one side
only and see red.

**Done when** changing any of those numbers on only one side fails CI.

## 2. Bot end-to-end tests over the real API (M)

**Why.** Unit tests per game are strong, but the bugs that cost days were in the joins between parts (for example the
End-game-versus-answer race). The studied ecosystem found the same: most of its real bugs came from suites that run a
real server and several real clients. This is also the safety net for the multi-game work (items 5 to 9).

**Build.**
1. New folder `backend/RandomRoom.Tests/EndToEnd/` using `WebApplicationFactory<Program>` (as `ApiTests` does) and real
   SignalR clients (`Microsoft.AspNetCore.SignalR.Client`, a test-only package; match the .NET 10 version).
2. A `Bot` class: creates a room through `POST /api/rooms`, claims each invite, joins, opens a hub connection with its
   token, keeps its latest snapshot (respecting `sequence`), and exposes `Act(action, payload)` over `POST /api/room/action`.
3. One strategy per game that plays a whole game to Completed using only what that bot is allowed to see. Start with
   three (Would You Rather, Trivia, Bingo), then the hidden-information games (Word Spies, Sketch Guess), then all 19.
4. Assertions every run: the game reaches Completed; every bot ends on the same session status (this catches the stale
   screen race); **no bot ever received another player's secret** (scan every pushed snapshot for the spymaster key, the
   drawer's word, other players' Bingo cards, the lie); reconnecting mid-game restores the same state.
5. Add race tests: End game at the same instant as an answer (many repetitions); a player joining while the host acts.
6. Rate limits: the join limit (20 a minute per address) blocks a many-bot run. Make the permit limits configurable
   (for example options `RateLimits:JoinPerMinute`, `RoomAdminPerMinute`, default unchanged) so the harness can raise them.
   This is a small production change; keep defaults exactly as today.
7. CI: run a small subset (3 games) on every pull request and everything nightly. Add the job to
   `.github/workflows/ci.yml`; mark long tests with a trait so they can be filtered.

**Test the harness.** Break one thing on purpose (return another player's key in a payload) and confirm the secret scan
fails; remove the sequence check in the bot's snapshot handling and confirm the stale-state assertion fails.

**Open decision.** Every PR, or nightly only. Recommended: subset per PR, full nightly.

## 3. Referee host, phase 1: the host does not play by default (L)

**Why.** Today the host is also a player in every game (the host is one of the seats). That gives a host who plays small
unfair powers: they control pace, and in a few games they decide scores. Some groups also want a neutral person who runs
the game and does not compete. Decision: **new rooms start with the host as referee (not playing)**, and a later step
(item 4) lets a host choose to play. Existing rooms keep today's behaviour.

**Already built (do not redo; it matters when a host does play, item 4):** Spin the Wheel (nobody awards themselves; when
the host spins, another player awards), Forbidden Words (only the judge can flag, because only the judge sees the card),
and a visible "Host decisions about scores" list in Spin the Wheel, Forbidden Words and Buzzer. See `DECISIONS.md`
("Host fairness").

**Design.**
- `Room.HostPlays` (bool). Existing rooms: `true` (backfill in the migration), so nothing changes for them. New rooms made
  through the UI send `false`. The service-level default stays `true` so the ~500 existing tests, which assume the host
  plays, keep passing; add `HostPlays` as an optional parameter of `CreateRoomRequest` (default `true`) and as an optional
  HTTP field.
- A **participant** is a seat that plays: all seats, minus the host when `!HostPlays`. Every game logic that lists
  players, rotates turns, counts answers or builds a scoreboard uses participants.
- The host seat still exists (token, PIN, recovery code, audit, host controls). The host just is not in the game.
- `MinPlayers`/`MaxPlayers` apply to participants. Seat cap stays 12, so a 12-player game has 11 participants plus the
  referee unless the host plays.
- The referee sees the whole game as a read-only view plus their controls (start, reveal, next, skip, end, award).

**Build.**
1. Migration: `Rooms.HostPlays` bool, default `true`. Domain `Room.HostPlays`.
2. `GameStore.PlayerNamesAsync(roomId)`: return participants (filter the host out when `!HostPlays`). Add
   `AllSeatNamesAsync` for the few places that need every seat (presence, audit). Fix the three direct counts
   (`RandomPickerEngine.cs:89`, `TriviaEngine.cs:218`, `:258`) to use participants.
3. Authorisation: a referee must not be able to play. Where an engine checks `players.Contains(actor)` it is already
   enforced; audit **every** action path in all 19 engines and add the check where an action is for participants only
   (answer, vote, buzz, spin, submit, mark, draw, guess). Host-only actions stay host-only. Return a clear 403 ("You are
   refereeing this game").
4. `RoomAdminService.CreateRoomAsync`: validate `MinPlayers`/`MaxPlayers` against participants, not seats. The UI message
   `playerCountIssue` (frontend `lib/players.ts`) must take the participant count.
5. `Services/RoomSnapshot.cs`: add `HostPlays`; `PlayerView` gets `Plays` (false for a referee). Update the frontend
   types (`lib/types.ts`).
6. Frontend: `GameScreenProps` gets `referee: boolean` (`isHost && !hostPlays`). Every game screen must handle it:
   hide answer/vote/buzz/mark/draw controls, show a short "You are refereeing" line, keep the host controls, and keep the
   scoreboard to participants. Do the games in batches that share a screen (the round games first; then Wheel, Bingo,
   Buzzer, story games, Two Truths, Guess Who, Word Spies, Forbidden Words, Sketch Guess, Random Picker).
7. `CreateRoomScreen.tsx`: the Players step explains "You are the host and referee. Add the people who will play." and
   shows the participant count against the game's range. The host seat is still entered (it is the first seat).
8. Per-game follow-ups (check each; some games gain a better role for a referee):
   - **Buzzer:** the host already judges and never buzzes; participants needed drop from 3 seats to 2 participants.
   - **Forbidden Words:** the referee should see the card and may flag a slip (the card must reach the referee's payload
     only when `!HostPlays`). Optionally make the referee the permanent judge, so only 2 participants are needed.
   - **Spin the Wheel:** the host is never the spinner, so the host always awards (`awardMode` stays `"host"`).
   - **Bingo, Name That, Two Truths, Guess Who, Survey Showdown:** the host calls, reveals or skips; exclude the host from
     card dealing, storyteller rotation, fact submission and voting.
   - **Word Spies, Sketch Guess, story games:** exclude the host from teams, drawer rotation and turn order.
9. Update `README.md` ("the host runs the game and, by default, does not play").

**Tests.**
- Per engine: with `HostPlays=false`, the host is absent from every list and scoreboard; a host action that plays is
  refused with 403; a game with exactly `MinPlayers` participants starts; with one fewer it is refused.
- Existing-room behaviour: `HostPlays=true` rooms play exactly as before (the existing suite is the proof).
- API: create with `hostPlays:false`, then check snapshot flags; migration test against a database with an existing room.
- Frontend: for each screen, render as referee and assert no play controls and the host controls present.
- Item 2's bots: add a referee bot to the end-to-end runs.

**Watch out.** Player-count rules show up in three places that must agree (server validation, `playerCountIssue`, the
engine `MinPlayers`); item 1's contract tests help. A referee who is also the only person who knows the setup (trivia
answers, Name That) is fine: that is the point.

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
several sessions, each game type); run the item 2 bots across a "Start new game" to prove nothing changed; a concurrency
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
anyone holding it can watch or only people the host admits. **Tests:** a spectator never receives a secret (reuse item 2's
secret scan); revoking the link disconnects them.

## 15. Before real use: tasks that are not code

1. **Review the East Africa trivia pack.** It is wired in (Trivia setup > Question set > East Africa, 20 questions) but
   nobody has checked the questions. Play it, check every answer, swap in questions your players know. Source:
   `backend/RandomRoom.Api/Games/Content/trivia-east-africa.json`; notes in `docs/trivia-east-africa-draft.md`. If you
   change the count, update `STARTER_PACKS` in `frontend/src/games/trivia/setup.ts` too (item 1 will check this).
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
6. **Commit or discard `QA_REPORT_2.md`** (the only untracked file) and merge the research branch if you want the report
   in `main`.
