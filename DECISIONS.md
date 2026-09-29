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
