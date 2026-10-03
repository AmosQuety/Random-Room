# Our app: "The Playground" (baseline for comparison)

Read on 2026-10-03 from `main` at `ca6d315` (the research branch adds only `docs/research/`). Paths are relative to the
repository root. This is a model in the same vocabulary used for the studied repositories: game contract, unit of
play, state sync, roles, persistence, operations, testing.

## 1. Shape

A web app: ASP.NET Core 10 API + EF Core + Postgres + SignalR (`backend/RandomRoom.Api`), React 19 + Vite frontend
(`frontend/`), served as one Docker container (`README.md`, "Deploy on Render"). Single server instance by design:
presence and the snapshot sequencer are in process memory (`README.md` "Notes"; `Services/RoomSnapshotSequencer.cs:9-13`).
Turn/phase based; no real-time simulation.

## 2. Game contract

Two halves, joined by a string key (`GameType`):

| Side | Contract | What it provides |
|------|----------|------------------|
| Server | `IGameEngine` (`backend/RandomRoom.Api/Games/IGameEngine.cs:10-59`) | `GameType`; `MinPlayers`/`MaxPlayers` (default 2..12, lines 16-19); `ConfigureRoomAsync(roomId, setup)` validates and stores setup, **called once at room creation** (21-26); `OnSessionCreatedAsync`, `OnSessionStartedAsync` (28-35); `HandleActionAsync(actor, action, payload)` (41); `IsSessionCompleteAsync` (44); `GetPayloadAsync` (public view) and `GetPayloadForAsync(viewer)` (per-player view, 46-55); `GetRoomPreviewAsync` for the unauthenticated join page (58). |
| Server, easy path | `IRoundRules<TPrompt>` (`Games/Rounds/RoundRules.cs`) composed into `RoundGameEngine`, registered with `AddRoundGame<,>()` (`Program.cs:62-68`) | parse prompt, view prompt (never with the key), validate answer, resolve round into summary + points; phases, hidden answers, deadlines, totals come from the shared engine. |
| Client | `GameModule<TSetup,TPayload>` (`frontend/src/games/types.ts:30-52`) | `key`, `name`, `description`, `hook`, `category`, `accent`, `Glyph`, `minPlayers`, `maxPlayers`, `defaultSetup`, `isSetupValid`, `setupIssue`, `toApiSetup`, lazy `SetupForm`, lazy `GameScreen`. |
| Registry | server: DI registrations in `Program.cs:50-68`; client: `GAMES` map in `frontend/src/games/registry.ts` | No descriptor file; discovery is compile-time. No versioning per game. |

There is no single descriptor: player ranges and setup limits exist once in the engine and again in the frontend module
(`DECISIONS.md` "Trivia limits" and "Trivia packs": "change both together"; `NEXT_STEPS.md` "Setup rules are written twice").

There is no shutdown hook on the engine: completion is `IsSessionCompleteAsync`, and the host can end a session
(`Services/GameSessionService.cs:88-97`).

## 3. Room / session model (unit of play)

- `Room` (`Domain/Room.cs`): `Slug`, `Title`, **`GameType` "Fixed for the room's lifetime"**, `HostPlayer` (a name),
  `RecoveryCodeHash`.
- `RoomPlayer` (`Domain/RoomPlayer.cs`): named seat, `PinHash`, one-time `InviteToken`, `TokenVersion`. Seats are fixed
  at creation (`docs/plan-switch-games-in-a-room.md`, "Seats are fixed at creation").
- `GameSession` (`Domain/GameSession.cs`): `Number`, `Status` Waiting/Active/Completed. **No game type** of its own.
- Setup: `RoomGameSetup` one JSON row per room (`Domain/RoomGameSetup.cs`: "Room-level: every session replays the same
  setup"), read by `GameStore.GetSetupAsync(roomId)` (`Games/Shared/GameStore.cs:44-51`). Random Picker and Trivia have
  their own room-keyed tables (`RoomChoices`, `TriviaQuestions`).
- Live state: `GameSessionState` one row per session: phase, round, deadline, claimant, JSON data, `Version` concurrency
  token (`Domain/GameSessionState.cs`). Player actions: append-only `GameEntry` with unique
  `(SessionId, Round, Kind, Player, Seq)` (`Domain/GameEntry.cs`; `DECISIONS.md` "Single submission is a unique index").
- Lifecycle (`Services/GameSessionService.cs`): `StartSessionAsync` (Waiting -> Active, host only, 75-86),
  `EndSessionAsync` (Active -> Completed, host only, 88-97), `StartNewSessionAsync` (needs Completed; creates session
  `Number+1` of the same game type, 99-123). Host check is `actor != room.HostPlayer` (162-168).

## 4. State sync

- Actions go in over REST (`POST /api/room/action`, `Endpoints/RoomEndpoints.cs:52`), state goes out over SignalR
  (`Hubs/RoomHub.cs:8`: "Server-to-client push only").
- After any change, `RoomBroadcaster.PublishAsync` builds **one snapshot per online player** and sends it to that
  player's group (`Services/RoomNotifier.cs:46-55`, 59-63). There is deliberately no room-wide send (line 5).
- Each snapshot carries a `Sequence`; builds for one room are serialised behind a semaphore and sequences start from the
  clock so they rise across restarts (`Services/RoomSnapshotSequencer.cs:4-37`). The client keeps the higher one
  (`frontend/src/lib/snapshots.ts`, `newerSnapshot`) and drops an action error once a newer snapshot arrives
  (`currentError`).
- Resync: full refetch on every (re)connect (`frontend/src/lib/useRoom.ts:84-115`). No acks, no deltas: every push is a
  full snapshot.
- Hidden information: `GetPayloadForAsync(viewer)`; the viewer-less view must be safe (`IGameEngine.cs:49-55`;
  `DECISIONS.md` "Per-viewer snapshots").
- Doc drift found: `Services/RoomSnapshot.cs:4` still says "The same view is sent to every participant", which is no
  longer true since per-viewer snapshots were added.

## 5. Timers and concurrency

- Deadlines are server time; clients get `TimerView(DeadlineAt, ServerNow)` and may send `tick`, honoured only if the
  server clock agrees (`Games/Shared/ServerTimer.cs:3-19`). No background tick loop.
- Phase moves through `PhaseGuard` (`Games/Shared/PhaseGuard.cs:17-40`).
- Concurrency: row lock `FOR UPDATE` on the session state (`GameStore.cs:176-184`), optimistic `Version`
  (73-86), single-UPDATE claims for buzzers (88-110), unique index on entries (114-138).

## 6. People and roles

- Two roles only: host (one named player, `Room.HostPlayer`) and player. Checked in services by name comparison
  (`GameSessionService.cs:165`, `RoomAdminService.cs:142,157,182`).
- Identity: per-room seats, no accounts. Invite link -> player sets PIN -> 12-hour signed token with `TokenVersion`
  (`README.md` "Auth model"; `DECISIONS.md` "PIN recovery").
- Host-only tools: start/end/new game, reset another seat (between games only), make recovery code, delete room
  (`Services/RoomAdminService.cs:137-213`). Append-only `RoomAuditEvents` (`Domain/RoomAuditEvent.cs`).
- No host hand-over, no spectators, no kick/ban beyond seat reset, no adding seats later (`NEXT_STEPS.md`
  "Known weaknesses"; plan doc).

## 7. Persistence, scores

- Everything in Postgres; restart-safe because live state is in rows, not memory (`GameStore.cs:24-28`).
- Scores are per game and per session; no room-wide or cross-session score (`docs/plan-switch-games-in-a-room.md`
  "Scores are per game").
- Built-in content: versioned JSON embedded in the assembly (`Games/Shared/ContentBank.cs:5-9`).
- Retention job deletes rooms inactive for `Room__RetentionDays` (default 30)
  (`Services/RoomRetentionService.cs:8-35`).

## 8. Operations

- Config: ASP.NET configuration (appsettings + environment variables such as `Room__JwtSigningKey`,
  `Database__AutoMigrate`, `Room__RetentionDays`). Refuses to start with a short signing key or an unmigrated database
  it may not migrate (`README.md` "Deploy on Render" step 3, "Database migrations").
- No console or admin tools beyond the host's in-room controls; rate limits on join/admin endpoints
  (`Program.cs:77-99`).
- CI: `.github/workflows/ci.yml`, on pull requests only: `dotnet test` with a Postgres service; lint, contrast check,
  build, vitest. No release workflow, no version stamping.

## 9. Testing

- Backend xUnit against real Postgres, throwaway database per test (`README.md` "Test"): 31 test files, 357 `[Fact]`
  and 55 `[Theory]` attributes (counted with grep; `NEXT_STEPS.md` reports 509 passing cases). Per-game tests
  follow a checklist: happy path, wrong phase, wrong actor, duplicate, invalid input, no leaks, completion (`README.md`
  "Adding a game" step 4). Deterministic clock and scripted random (`backend/RandomRoom.Tests/GameHarness.cs`).
- Frontend Vitest: 42 test files.
- End-to-end: none in the repo. Two manual/agent QA audits with Playwright scripts kept outside the repo
  (`docs/QA_PROMPT.md`; `NEXT_STEPS.md` "Put browser tests in the repo").

## 10. The plan document and the decisions it leaves open

`docs/plan-switch-games-in-a-room.md` proposes (A) moving `GameType` and setup from room to session, with a host
"Change game" between games, or (B) linked rooms that copy seats and PIN hashes. Order: contract tests, data-model-only
migration, host change-game, add a player, tonight's scoreboard, next-game vote.

Open decisions it lists or implies:
1. Host-only switch, or player proposals/votes from the start? (plan, "Open questions" 1)
2. Scope of "tonight's scoreboard": per room, per evening, never reset? (2) And how to compare games that score
   differently (placings vs points) ("Later phases").
3. Adding players mid-evening wanted? (3)
4. Is B acceptable if A is too large? (4)
5. Next-game vote rules: host veto, ties, can a game repeat straight away ("Later phases").
6. Whether `Room.GameType` stays as "current game" or is dropped for the latest session's ("What A needs" 3).
7. Not stated but implied: what happens to the recovery code and host role over a long evening (NEXT_STEPS idea 6).

Note: the plan's closing line says `game-playground` "rotates maps inside one game, not different games". The research
below tests that statement.
