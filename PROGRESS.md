# Progress

Last updated: 2026-11-01 17:45

## Now
NEXT_STEPS items 1 to 5 are merged to `main` (PRs #10 to #12) and the owner's manual Render deploy FAILED at startup on 2026-11-01 (about 14:34 UTC): the app could not resolve the database's internal host name (`Name or service not known`) on its very first connection, so no migration ran and no schema or data was touched. Cause not yet confirmed. Leading suspicion, to verify in the Render dashboard: the free Postgres database no longer exists or is suspended (Render free databases expire after a fixed period). Work is paused until production is back. Kahoot research is finished and its ideas are scheduled below (K1 to K8).

## Next (in this order, and why)
0a. **Fix production's database connection** (owner, in the Render dashboard): does the `random_room` database still exist, and what is its status or expiry? Is the web service in the same region as the database? Did the connection-string environment variable change? If the free database expired, create a new one, point the service at its internal URL, and deploy: the app creates the whole schema on an empty database at startup (this will also be the first real run of every migration). Rooms are short-lived, so losing old rooms is acceptable, but confirm that with the owner first.
0b. **Smoke test production** once it is up (owner): create a room, play a short game on a phone, press "Start new game", try a referee game. This also covers the item 5 migration and the referee screens, which nobody has looked at in a real browser. If it finds a problem, fix it before anything below.
1. **Item 3 follow-up** (S): one test per engine that a referee can still run it (a typo in `RefereeActions` would otherwise lock the referee out silently). Protects what just merged. Answer the Buzzer `Plays` question first.
2. **Quick wins, frontend only, no migration** (S each; can go in any gap):
   - **K1 Podium end screen**: top 3, ties share a place, honours reduced motion. Reuse the ranking rule in `frontend/src/lib/ranking.ts`; item 8 will reuse the same placings, so settle the tie rule here.
   - **K6 Colour plus shape on answer buttons**: not colour alone (helps accessibility, which we already require). Check contrast with `npm run check:contrast`.
3. **Item 6 "Change game"** (M, no blockers: item 5 is merged): the "play again or choose another game" panel. Prerequisite from the review: read the game from the session everywhere and update `Room.GameType` and the session together. Do this before the new games, because more games only matter once a room can switch between them.
4. **Item 7 "Add a player"** (S-M, only if wanted): lets small rooms pick games with higher minimums such as Word Spies.
5. **Item 8 "Tonight's scoreboard"** (M): combine placings, not points. Needs the scoring rule and reset scope from the owner. Reuses K1's tie rule.
6. **K8 Between-question leaderboard and a host results report** (S-M): first check what the screens already show; the report can read the stored results item 8 adds. (K8 comes from memory of Kahoot, not from the research.)
7. **Trivia v2** (M), grouped because they share the Trivia setup and answer path (setup is now per session):
   - **K5 Typed answers** (reuse the answer matching already used in Name That; decide spelling tolerance).
   - **K2 Optional scoring modes**: "accuracy only" (today) stays the default; opt-in speed bonus and opt-in streak. Time is measured by the server, so a slow connection loses points: OFF by default, and the setup form must say so. Extend the server-facts contract tests for any new limit.
8. **New games, one at a time** (M each): **K3 Slider** (guess a number, closest wins) first, then **K4 Puzzle** (put answers in order; decide partial credit). Each needs an engine with `RefereeActions` and `MinPlayersWithReferee`, a frontend module, tests, the server-facts fixture, and an end-to-end bot strategy.
9. **Item 9 "Next-game vote"** (M, needs item 6): richer once there are more games to vote between.
10. **K7 Team scoring beyond Word Spies** (L, design decision first): team totals and a team scoreboard in more games; builds on the referee/participant model and item 8.
11. **Any gap:** items 10 to 13 (hand-over, new-version banner, host status view, reuse a past setup), and the older gaps in `Gaps_Bugs.md`.
12. **Before inviting real users:** item 15 (review the East Africa trivia pack, load test about 50 rooms of 8, real phones with a screen reader, backups). Item 1 (`GET /api/games`) and item 14 (spectator link) only if asked for.

## Decided not to borrow from Kahoot (and why)
- Open game PIN and nickname generator: our invite-only seats are a deliberate security choice (Gaps_Bugs G1).
- Image-heavy "pin it" questions and background music: bandwidth cost for slow connections.
- Confidence mode: low value for the extra rules to explain.
- Speed and streak scoring are allowed only as opt-in: Kahoot users ask to switch them off, a Kahoot staff comment says the streak bonus should be off by default, and speed scoring conflicts with our low-bandwidth and fairness rules (the Buzzer already favours the faster connection).

## Done recently
- 2026-11-01: items 1 to 5 merged to `main` via PRs #10, #11, #12 (shuffle and contract tests; bot end-to-end tests; referee host; host chooses to play; sessions own their game type and setup). Backend 771 and frontend 397 tests passed at the last check.
- 2026-11-01: Kahoot research finished (sources in the chat; official Kahoot support pages returned 403, so exact scoring numbers and question limits are unconfirmed).

## Open questions / blockers
- Production database: does it still exist? (See Now and 0a.) Should the app retry the database connection with a backoff at startup instead of exiting? Proposal only, not built.
- Smoke test result (owner).
- Buzzer room where the host plays: the snapshot says `Plays=true` for a host who never buzzes. Intended?
- Wanted at all? Item 1 (`GET /api/games`), item 7 (add a player), item 14 (spectator link).
- Is the production Postgres 13 or later? The item 5 migration needs it. Deploy is manual on a Render free instance, so the old and new versions may briefly overlap; deploy in a quiet window (see DECISIONS.md, item 5).
- Scoring rule and reset scope for item 8; tie rule for K1.

## Context to remember
- Tests need Postgres. Port 5433 is another project's container, so a throwaway one runs as `randomroom-pg-test` on 5434. Use `DOTNET_ROOT=/usr/share/dotnet RANDOMROOM_TEST_SERVER="Host=localhost;Port=5434;Username=postgres;Password=postgres;Database=postgres"` and `dotnet test backend/RandomRoom.slnx --filter "Suite!=Nightly"`.
- Refresh the contract fixture after a deliberate change: `UPDATE_SERVER_FACTS=1 dotnet test backend/RandomRoom.slnx --filter ServerFactsTests`.
- Repo-wide `dotnet format` exits 2 because of 3 old xUnit warnings in Buzzer/Bingo/RoundGame tests; check formatting with `--include` on touched files.
- `gh` is not installed: open PRs on GitHub. Merged branches are redundant and can be deleted on GitHub.
- This file was the build-phase checklist before; everything below the line is that older content, unchanged. This tracker block is uncommitted working notes.

---

## Summary (updated at the end of each phase)

**Status: Phase 1 (design overhaul) done. Phase 2 (shared primitives) done. Phase 3 (games): every game in the brief is built as a registered plugin, see the checklist. Nothing is blocked.**

**Built (19 games, all registered plugins with backend and frontend tests):** Random Picker, Trivia, Would You Rather,
This or That, Most Likely To, Never Have I Ever, Survey Showdown, Two Truths and a Lie, Guess Who Wrote It, Name That
Song or Movie, Spin the Wheel, Buzzer Round, Bingo, Fill-in Stories, One-Word Story, Fortunately/Unfortunately, Word
Spies, Forbidden Words, Sketch Guess.

**Blocked:** nothing.

**Limitations:** the buzzer favours the faster connection; Sketch Guess drawing is pointer-only and an abnormally
ended session leaves its last round's strokes in the state row; timers are checked lazily when a client sends `tick`;
built-in content is English only.

**Suggestions:** a background sweeper for expired rounds, a keyboard-drivable drawing mode, localized content banks,
end-to-end browser tests against a real API.

Run 1 could not build the backend (NuGet was unreachable). Run 2 has NuGet, so the backend compiles and its tests
run against a real Postgres. Every game below is a full plugin: backend engine, frontend module, tests on both sides.

## Environment notes

- Installed: `dotnet-sdk-10.0` (apt), `@fontsource-variable/fraunces` (npm). Postgres 16 was already present;
  started on port 5433 with throwaway credentials. `.env` was created locally and is git-ignored.
- Not possible: `dotnet restore` / `dotnet test` / `dotnet ef` (NuGet blocked), Docker daemon (none).
- Screenshots: Playwright with the preinstalled Chromium against the Vite dev server, with the API and the
  SignalR handshake mocked at the network layer (no backend could run). Layouts were checked at 360px and 1280px
  and `document.documentElement.scrollWidth` was asserted equal to the viewport width on the main screens.

## Baseline (before any change)

| Check | Result |
| --- | --- |
| `npm run lint` | 1 warning (`react/set-state-in-effect` in `useRoom.ts`), pre-existing |
| `npm run build` | passes, JS 90.6 kB gzip |
| `npx vitest run` | 8 tests pass |
| `dotnet test` | could not run (NuGet blocked) |

## Checklist

| Item | Status | Note |
| --- | --- | --- |
| Environment bootstrap | done | Postgres 16 on 5433, .NET 10 SDK, npm ci |
| Phase 1: design overhaul | done | Wordmark hero + persistent header, stepped create flow, categorized picker, per-game accents, states, motion, a11y |
| Phase 2: shared primitives | done | Per-viewer snapshots, generic state/entry tables, phase guard, server timer, answer matching, content banks, round engine + shared round screen and setup form. See `DECISIONS.md` |
| Would You Rather | done | backend + frontend tests, smoke-tested against the running API |
| This or That | done | backend + frontend tests, smoke-tested against the running API |
| Most Likely To | done | backend + frontend tests, smoke-tested against the running API |
| Never Have I Ever | done | backend + frontend tests, smoke-tested against the running API |
| Family Feud-style (Survey Showdown) | done | individual scoring, board only sent at reveal |
| Trivia upgrades (categories, bank, time limit) | done | additive: two nullable columns, starter bank of 30, server-checked per-question deadline; all original trivia tests still pass |
| Two Truths and a Lie | done | custom engine on the shared primitives; the lie is only ever in the storyteller's own payload until the reveal |
| Guess Who | done | text facts only; authorship only in the author's own payload until the reveal; guess progress is a count, not a per-player list (a list would single out the author) |
| Song/Movie intro guessing (Name That) | done | host clues plus optional https link (new tab, no iframe); host-set accepted answers marked server-side; first correct by receipt order gets a bonus |
| Spin the Wheel | done | server picks the segment (scripted random in tests); client only animates, no motion under reduced-motion; host awards a point per spin; built-in bank of 30 tops the wheel up to 8 |
| Buzzer | done | first buzz decided by one atomic UPDATE (`TryClaimInPhaseAsync`), outside the session lock; host judges so needs 3 players; wrong answer locks the player out; 30 built-in prompts. Race tested with 3 concurrent contexts and 3 concurrent HTTP requests |
| Bingo | done | 5x5 cards, distinct per player, dealt server-side and sent only to their owner; server draws the call order; marks must be called items; claims are checked against real lines; concurrent claims give one winner. 40 built-in words |
| Mad Libs (Fill-in Stories) | done | round engine + `ViewPromptFor` hook (additive); blanks shared out by player order, story text only sent at reveal; words rendered as plain text; 30 built-in stories |
| One-word story | done | shared turn-based `StoryChainEngine` (rules injected); one word per turn validated server-side; 30 built-in openers; contributions scored 1 each |
| Fortunately/Unfortunately | done | same engine; server adds the alternating lead-in and strips a typed one; each entry records its own lead-in so skips do not shift it; 30 built-in openers |
| Codenames-style (Word Spies) | done | teams dealt at random, one spymaster each; key only in spymaster payloads (everyone once over); assassin and all rules server-side; leak tests over every role and the public view; 132 built-in words |
| Taboo-style (Forbidden Words) | done | card only in the describer's and judge's payloads (leak-tested across every role and the public view); judge (next player) or host flags; server matches guesses, caps guesses per round, server-checked timer; 40 built-in cards |
| Pictionary-style (Sketch Guess) | done | word only in the drawer's payload (leak-tested for every role, the public view and the preview); stroke, point, batch and total caps plus a per-drawer rate limit enforced server-side; drawing kept only for its round; atomic first-correct-guess wins under concurrency; 105 built-in words; canvas frontend with pointer events, palette, pen sizes, undo, clear |
| Full-diff review | done | Checked for hidden-info leaks (per-role tests on every hidden-info game), secrets, committed env files and stray tool-identity text: none found. Only the three run-1 commits carry a different author (see Summary) |

## Phase 1 verification

- `npm run lint`: 1 warning, the pre-existing one. `npm run build`: passes, JS 95.1 kB gzip (fonts self-hosted, latin subset 37 kB).
- `npx vitest run`: 26 tests pass (18 new: Scoreboard, ranking, player-count rules, GamePicker with 20 games, create flow).
- `npm run check:contrast`: 44 text and non-text pairings pass WCAG AA (script reads the real tokens from `index.css`).
- Screenshots: `docs/screenshots/`.
