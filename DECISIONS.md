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
