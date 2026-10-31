# Decisions

Judgment calls where the brief was silent, each with a one-line reason.

- **Self-hosted Fraunces via `@fontsource-variable/fraunces`.** One added npm dependency: no third-party font requests, works offline, deterministic screenshots, and only the 37 kB latin subset is fetched.
- **Create flow stays on `/`.** The server only falls back to `index.html` for `/room/*`; a new `/new` route would need a backend change that could not be built.
- **Stepped flow (Game, Setup, Players) with a sticky action bar.** Keeps each step short on phones and puts the primary button where a thumb is.
- **Game picker uses native radios.** Arrow keys, form semantics and screen-reader behaviour come free; selection is shown by a check badge and heavier shadow, not colour alone.
- **Search appears only above 8 games.** Below that, scanning the grid is faster; the threshold is a single constant.
- **Random Picker is filed under "Reflex & Chance", Trivia under "Quiz".** Spin the Wheel (same shape) is listed under reflex in `games_to_add.md`.
- **Per-game accent through `data-accent` and CSS variables.** One attribute re-themes a room; Tailwind utilities (`bg-accent`, `text-accent-ink`) stay static so nothing needs safelisting.
- **`GameModule` extended additively** (`hook`, `category`, `accent`, `Glyph`, `minPlayers`, `maxPlayers`); `SetupForm` and `GameScreen` may be `React.lazy` components so each game is its own chunk.
- **Tomato darkened to `#b32f16`.** The original failed 4.5:1 on the deeper paper tint used for sections.
- **Mustard never used as text.** It fails on paper; `mustard-ink` is used where a mustard-family text colour is needed.
- **Join screen separates "not found" (404) from "could not reach the server".** The old screen said "not found" for every failure, which sent people chasing a correct link.
- **`useRoom` reports a failed first load** with a retry, instead of an endless loading state.
- **Shared `Scoreboard` extracted now** (used by Trivia) so later scoring games have one component and one ranking rule (ties share a rank, next rank skips).
- **Minimal CI workflow added** (`.github/workflows/ci.yml`): none existed, and the brief allows one for `dotnet test`, lint, build and vitest on pull requests. It adds the contrast check.

## Shared game primitives (backend)

- **`IGameEngine` gained two default methods, nothing was changed.** `OnSessionStartedAsync` and `GetPayloadForAsync(viewer)` default to the old behaviour, so Random Picker and Trivia are untouched. Hidden-information games (Codenames, Guess Who, Pictionary) need a per-player view, and the old seam could only return one payload for the whole room.
- **Per-viewer snapshots; the viewer-less snapshot is the safe one.** `RoomBroadcaster` sends each online player their own snapshot through a per-player SignalR group. The public preview and any room-wide view never carry secrets.
- **Composition over inheritance.** Engines use small shared helpers (`GameStore`, `PhaseGuard`, `ServerTimer`, `AnswerNormalizer`, `ContentBank`) instead of a base class, so an engine only takes what it needs.
- **Three generic tables, not one table per game.** `RoomGameSetup` (JSON setup), `GameSessionState` (phase, round, deadline, claimant, JSON data, version token) and `GameEntry` (append-only per-player entries). One additive migration covers every game; games with truly relational needs can still add their own.
- **Phase flow is `collecting -> revealed -> collecting | complete`,** enforced in one place (`PhaseGuard`) and raising `RoomRuleException`.
- **Single submission is a unique index,** `(SessionId, Round, Kind, Player, Seq)`, so a double-tap or replayed request cannot count twice, whatever the application code does.
- **Deadlines are server time.** Clients only receive a countdown view; at zero a client sends a `tick` and the server honours it only if its own clock agrees. No background service is needed and a restart loses nothing.
- **Buzzer ordering is by server receipt.** The first writer wins through one atomic `UPDATE ... WHERE claimant IS NULL`. Limitation: a player with a slower connection can lose to a faster one even if they tapped first; the game is fair to the server's clock, not to the players' latency.
- **Built-in content is embedded, versioned JSON** so lists are reviewed like code and are never fetched at run time.

## Phase 3 games

- **Generic game names, not trademarks.** Survey Showdown, Guess Who Wrote It, Name That Song or Movie, Fill-in Stories, Word Spies, Forbidden Words and Sketch Guess describe the mechanic without borrowing a brand.
- **Accents are shared.** The theme has only five accent tokens, so several games reuse one; the glyph and category are what tell them apart.
- **Story games share one engine.** One-Word Story and Fortunately/Unfortunately differ only in how a turn is validated and shown (`IChainRules`), so they share `StoryChainEngine`.
- **`ViewPromptFor` hook on the round engine.** Fill-in Stories must show each player only their own blanks; the hook lets a game reshape the prompt per viewer without forking the engine.
- **`GameStore.TryClaimInPhaseAsync`.** Buzzer claims are a single UPDATE that also checks the phase, so a buzz can never land on a round that is not open, with no lock held for the race itself.
- **Sketch Guess: REST in, SignalR out.** Actions (including stroke batches) go through `POST /api/room/action`; the hub stays push-only and fans the per-viewer snapshot out. One path means one place for authentication, validation and rate limits.
- **Sketch Guess bounds.** At most 20 strokes per batch, 400 strokes and 6000 points per round (a long continuous drag is cut into a stroke every 500ms, so the stroke cap alone would be reached in a couple of minutes), 200 points per stroke, a 100ms gap between canvas actions per round; the client batches every 160ms to stay inside it.
- **Sketch Guess keeps nothing beyond its round.** Strokes live in the session state row and are cleared when the next round starts and when the game completes. Limitation: if a host ends a session abnormally, the last round's strokes stay in that row until the room is deleted.
- **Drawing is pointer-only.** A canvas has no keyboard equivalent for freehand drawing; the drawer can skip the word, and guessers (who never draw) get a full keyboard path. Noted as a known accessibility limit.

## PIN recovery

- **Reset is host-only, between games, and never on the host's own seat.** Mid-game the host could claim the emptied seat and see that player's secrets; and a reset of the host's seat would need someone else to hold that power.
- **A per-seat `TokenVersion` is checked on every request.** Tokens last 12 hours, so without it a reset would leave the old device signed in. The check is one indexed query in `OnTokenValidated`; tokens issued before the column existed count as version 0.
- **The reset also cuts live connections.** The old device is told (`seatReset`), removed from its SignalR groups and from presence, so it receives no further snapshots.
- **An append-only `RoomAuditEvents` table**, separate from game state and logs, records who reset which seat and when. It is rejected for update or delete by `RoomDbContext` and is removed only with its room.

## Invites

- **QR codes use `qrcode-generator` (MIT, no dependencies), loaded only when a host presses "Show code".** Error correction and mask selection are easy to get subtly wrong, so a mature library beats a hand-written encoder. It adds about 7.6 kB gzipped as its own chunk and nothing to the main bundle. The code is drawn in the browser as inline SVG, so a private link never reaches a third party. It is always black on white with the standard 4-module quiet zone, whatever the theme.
- **Share uses the Web Share API only where the browser has it.** Closing the sheet is not an error; any other failure falls back to copying the link.
- **No shared "pick your name" link.** Anyone holding it could claim any unclaimed seat, which weakens one-link-per-seat.

## Trivia limits

- **6 options per question, 100 characters per option, 300 per question, 50 questions per room (your own and the starter ones together).** Chosen to fit a phone screen and a round that stays fun; the server enforces them with a 400 that names the limit, and the setup form mirrors them so a problem shows on the setup step. The constants live in `TriviaEngine` and `games/trivia/setup.ts`; change both together.

## Retention

- **Rooms with no activity for `Room__RetentionDays` (default 30) are deleted, with everything in them.** A daily background job, first run a minute after start. 30 days is a placeholder for a personal-project database; set it to whatever period the deployment is allowed or required to keep data. 0 turns the job off.
- **Activity** is a room created, a game created, started or ended, or a recorded action in a game. An old room that is played again is kept.
- **The job is the one place allowed to delete recorded answers and picks.** They are append-only while their room lives, and their foreign keys refuse a cascade, so the job deletes them first with `ExecuteDelete` (which bypasses the context's immutability guard on purpose) and then the room, in one transaction, 100 rooms at a time.
- **A failed run is logged and retried at the next run.** Deleting is idempotent, so two instances running it at once is harmless.

## Migrations at startup

- **The app migrates only when allowed.** `Database__AutoMigrate=true` always migrates; `false` never does; unset migrates only a database on this machine (`localhost`, loopback addresses, a unix socket). A local `.env` pointing at a shared or production database therefore cannot change its schema by accident.
- **When not allowed and the database is behind, the app refuses to start** with a message that says how to fix it, rather than running against a missing column. When the schema is current it starts normally.
- **Production sets `Database__AutoMigrate=true`.** One instance, additive migrations, so applying them at startup is simple and safe. If you ever run several instances or want a review step, turn it off and run `dotnet ef database update` from the deploy pipeline.
- **Retention stays at 30 days** (`Room__RetentionDays`), the default, confirmed.

## Trivia packs

- **Starter questions come in named packs** (`starterPack` in the setup: `general`, the default, or `east-africa`). The server maps the name to an embedded bank (`TriviaEngine.StarterPacks`), rejects unknown names with a 400, and takes the question count from the chosen bank. A setup with no pack behaves as before.
- **The pack's size is also written in the frontend** (`STARTER_PACKS`), the same way the general set's size always was, so the form can offer sensible counts. Both places must change together.
- **East Africa is unreviewed content.** Written from general knowledge, shipped on request so it can be tested; review it before relying on it (`docs/trivia-east-africa-draft.md`).

## Database encryption

- **TLS is required for any database that is not on this machine.** Npgsql's default (`Prefer`) falls back to plain text if encryption is blocked, which on the open internet lets someone on the path read the password and every room's data. The same "local" test as the migration guard decides (`localhost`, loopback, a unix socket), so local development is unchanged.
- **An explicit choice always wins**: `?sslmode=...` on a URL or `SSL Mode=...` in a key=value string. A value that is not recognised never weakens the default.
- **Not verified against a real hosted database.** The rule is covered by tests and local startup only; the first connection to Render, Neon or Supabase should be checked by hand. Whether a host's certificate validates under `Require` depends on the host; `Trust Server Certificate=true` is the escape hatch.

## Telling people how long rooms are kept, and deleting a room

- **The retention period is said where people decide to take part**: on the create screen's Players step, on the room-created card, and on a room's join page ("Rooms are deleted after N days without play"). The create screen reads it from `GET /api/config` and the join page from the room preview. If the server cannot say (or it is 0), nothing is shown rather than something wrong.
- **The host can delete the room at any time** (`DELETE /api/room`, host only, behind a confirm). It uses the same deletion as the retention job (`RoomDeletion`), so there is one meaning of "delete a room". Everyone connected is told (`roomDeleted`) and returned to the join page with "The host deleted this room"; their tokens stop working at once because their seats no longer exist.
- **A deleted room leaves nothing behind**, including its audit rows, which are removed with it. This is deliberate: deleting is the privacy option.

## Host recovery code

- **The host's seat is recovered with a code, not by anyone else.** Another player resetting the host's seat would make the host's power depend on someone else, so the host keeps their own key: a 16-character code (80 bits, an alphabet without look-alike letters, typed case- and dash-insensitively) shown once when the room is made. Only a PBKDF2 hash is stored, like a PIN.
- **Using it spends it.** Recovery empties the host's seat, signs out the old device (token version), cuts its live connection, and returns a one-time link for a new PIN plus a replacement code, so a host is never left without one. Spending is a conditional update, so two uses at once cannot both succeed.
- **The recovery page makes the host confirm they saved the new code** before it lets them carry on, because the old one is gone.
- **Guessing is throttled like PINs** (`/recover` shares the join limiter, 20 a minute per address), a wrong code and an unknown room get the same answer, and a room with no code still pays for a hash check.
- **The host can replace their code from inside the room**, which is also how rooms made before codes existed get one. Making and using a code are recorded in the audit table (the code itself never is).
- **Whoever holds the code is the host.** That is its purpose; the screen says not to share it.

## Host fairness (when the host also plays)

- **The host is a player today, so the places where a host decides a score are checked.** Spin the Wheel: nobody gives themselves the point. The host gives it, unless the host is the one who spun, in which case any other player may (the spin payload says which: `awardMode`). Forbidden Words: only the judge can flag a slip, because only the judge sees the card; the old blind flag by the host is gone. Buzzer: every point is the host's judgement, which is its design (the host never buzzes).
- **Host decisions about scores are shown to everyone**, not hidden: a short "Host decisions about scores" list under the scoreboard in those three games (`HostScoreNote`: round, player, points, reason). It includes a decision to give no point (the host moving on without awarding), because withholding is also a way to tilt a board.
- **This is not the end state.** The intended model is a referee host who does not play by default, with an option to play (see `NEXT_STEPS.md`, items 3 and 4). These rules stay in force whenever a host does play.

## Referee host (phase 1, backend)

- **`Room.HostPlays`**, a bool. The migration adds it as `NOT NULL DEFAULT true`, so every existing room keeps a host who plays. The model sets no default on purpose: with an EF default of `true`, a `false` would be treated as "not set" and silently written as `true`. The service-level `CreateRoomRequest.HostPlays` and the HTTP `hostPlays` field both default to `true`; the frontend will send `false` for new rooms.
- **A participant is a seat that plays**: every seat, minus the host when `!HostPlays` (`Games/Shared/Participants.cs`). `GameStore.PlayerNamesAsync` now returns participants, so every engine that lists players, rotates turns, deals cards, forms teams, counts answers or builds a scoreboard leaves the referee out without per-engine changes. The direct `RoomPlayers` counts in Random Picker (completion) and Trivia (all answered, the answered list) use participants too.
- **Seat places stay on seats**: the snapshot's player list (presence, `Claimed`, and the new `Plays` flag), the room preview, sign-in, seat reset, recovery, deletion and audit still read `RoomPlayers`. No caller needed a separate `AllSeatNamesAsync`, so it was not added.
- **Rooms where the host plays are not range-checked again at start or new game.** They were checked when the room was made and must behave exactly as before; only a referee room, whose participant count differs from its seat count, is checked at those points. (Found in review: the first version checked every room, which could refuse "Start new game" in an older room.)
- **Player ranges apply to participants**; the seat cap stays 12, the referee included (11 participants plus a referee). Checked at room creation and again when a game or a new session starts (`ParticipantRange`). Buzzer is the one game where a host who plays never plays either, so it has `MinPlayersWithReferee = 2` (3 seats when the host plays, 2 participants with a referee). The server-facts fixture does not carry this yet: that is the frontend half.
- **One central check, deny by default.** `GameSessionService.PerformActionAsync` refuses (403, "You are refereeing this game, so you do not play.") any action from a referee host that the engine has not listed in `IGameEngine.RefereeActions`. The default is empty, so a new game lets a referee do nothing until it says otherwise. Engines keep their own host-only checks for those actions.

| Game | Referee may send | Participants only (refused for a referee) |
|---|---|---|
| Random Picker | none | trigger |
| Trivia | tick | answer |
| Two Truths | reveal, next, skip | submit, vote |
| Guess Who | begin, reveal, next | submit, guess |
| Spin the Wheel | award, next | spin |
| Buzzer | open, correct, wrong, skip, next | buzz |
| Bingo | call, end | mark, bingo |
| Word Spies | end | clue, guess, pass |
| Forbidden Words | reveal, next, tick, flag | guess, skip |
| Sketch Guess | reveal, next, tick | strokes, undo, clear, guess, skip |
| One Word Story, Fortunately | skip, end | add |
| Name That, Mad Libs, Would You Rather, This or That, Most Likely To, Never Have I Ever, Survey Showdown | reveal, next, tick | answer |

- `tick` only asks the server whether a deadline has passed, so a referee's screen may send it.
- **Hidden information.** Forbidden Words is the only game where a referee sees more than a guesser: the card (`role: "referee"`), so the referee can flag a slip; it shows in "Host decisions about scores" as "The referee flagged a slip". The describer and judge still rotate among participants (making the referee the permanent judge was optional and is not done). In every other game the referee gets the same view as a participant who holds no secret: no Bingo card, no Word Spies key, no Sketch Guess word, no Two Truths lie, no Guess Who authorship, no answers before a reveal. Spin the Wheel: the referee never spins, so `awardMode` is always `"host"`.

## Referee host (phase 1, frontend)

- **One flag, wired once.** `RoomScreen` computes `referee = isHost && hostPlays === false` and passes it to every game screen as `GameScreenProps.referee`. A snapshot with no `hostPlays` (an older server) means the host plays.
- **Screens get participants only.** `RoomScreen` filters the player list with `participants()` (players whose `plays` is not `false`) before handing it to a game, so every "N players" count, progress list and scoreboard leaves the referee out without per-screen filtering. `SeatControls` still gets every seat.
- **What a referee sees.** The shared `RefereeNote` ("You are refereeing: you run the game and do not play", plus one sentence of what they can do) on every game. No answer, vote, buzz, mark, guess, draw or write control; the host controls stay (start/end in `HostControls`, and reveal, next, skip, award, call, end per game, matching the server's `RefereeActions` table). Trivia shows the options as plain text. Two Truths shows the statements read-only. Forbidden Words shows the card and a "Flag a slip" button for `role: "referee"`.
- **The create flow** always sends `hostPlays: false`. The Players step counts participants as the names entered minus the host, and checks them against the game's range; Buzzer uses `minPlayersWithReferee` (2). `withReferee()` swaps that minimum into the range so `playerCountIssue` stays a plain function. A duplicate-name message now wins over the count message, because it is the more specific fix.
- **Server facts** now include `minPlayersWithReferee`; `serverFacts.test.ts` compares it with the registry.
- **Decision for the owner:** the Create flow offers no choice yet (that is item 4, "the host can choose to play"). The host select still lets the creator name someone else as host; that person then referees.

## Server facts the frontend copies

- **Some facts are written twice, once on the server and once in the frontend**: the game keys, each game's player range, the trivia limits and the starter-pack sizes. Removing the copy would put a request in front of the game picker, so the copy stays and is guarded instead. A mismatch has caused bugs before (the accent-duplicate checks, three times).
- **The server writes the facts down; the frontend compares against them.** Backend `ServerFactsTests` reads the registered engines and the trivia constants and checks them against a committed fixture, `frontend/src/games/__fixtures__/server-facts.json`. The frontend's `games/serverFacts.test.ts` compares its registry, ranges, limits and pack sizes with that fixture. The two sides never talk to each other at test time, so each CI job (backend, frontend) stays independent.
- **A comparison that cannot happen is a failure, not a pass.** A missing fixture, an empty list of games or no trivia facts all fail the frontend suite, and a missing or stale fixture fails the backend test. This is the lesson from the research: the studied ecosystem had a comparison test that passed silently when it could not compare.
- **Refreshing is a deliberate act**: `UPDATE_SERVER_FACTS=1 dotnet test backend/RandomRoom.slnx --filter ServerFactsTests` rewrites the fixture, and the change shows in the diff for review. The environment variable is the only way the test ever writes a file.
- **`TriviaEngine.MaxCategoryLength` became public** (it was private) so the test can read it instead of repeating the number a third time.
- **Whether to serve the facts from `GET /api/games` instead is still open**, see `NEXT_STEPS.md` item 1.

## Bot end-to-end tests

- **Bots are real clients.** A `Bot` signs in over HTTP, listens on `/hubs/room` (WebSocket through the test server, token in the query string as a browser sends it), acts over `POST /api/room/action`, and keeps the newest snapshot by `sequence`, as `useRoom.ts` does. The suite lives in `backend/RandomRoom.Tests/EndToEnd`.
- **A strategy per game, not a script per test.** `IGameStrategy` plays one step of a game and says what is secret in it, so adding a game (Word Spies, Sketch Guess, the rest) means adding a strategy; `Bot`, `GameTable` and `GamePlay` do not change. Three exist: Would You Rather (a player's pick until the reveal), Trivia (the right option until its question closes), Bingo (other players' cards and the internal call order).
- **The secret scan reads everything a bot was ever sent**, including snapshots it ignored as stale, not just the last one.
- **Every run checks** that the game completes, that every bot ends on the same status after a short quiet period, that nothing secret arrived, and that reconnecting restores the room.
- **Rate limits are options** (`RateLimits:JoinPerMinute`, `RateLimits:RoomAdminPerMinute`), defaults 20 and 40 as before, read per request. The harness raises them because every bot shares one address; a test pins the defaults.
- **Pull requests run a subset, the nightly run runs everything.** Tests carry `Category=EndToEnd`; the long ones (race repetitions, twelve-player games) also carry `Suite=Nightly`. CI runs `--filter "Suite!=Nightly"` on pull requests and `.github/workflows/nightly.yml` runs all tests on a schedule and on demand.
- **Finding:** without the sequence check in the bot, ordinary games hang on a stale screen, because a response to a player's own action often arrives after the newer push. That confirms why snapshots carry a sequence.
