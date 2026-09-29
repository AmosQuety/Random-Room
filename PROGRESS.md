# Progress

## Summary (updated at the end of each phase)

**Status: Phase 1 (design overhaul) done. Phase 2 (shared primitives) backend done; games in progress.**

(The paragraphs below describe run 1, where NuGet was unreachable. It is reachable now and the backend builds and tests.)

The unattended cloud session this ran in could reach npm and GitHub but not NuGet
(`api.nuget.org` is denied by the session's egress policy; the proxy answers 403 to CONNECT and its
status endpoint records it). `dotnet restore` therefore cannot download EF Core, Npgsql, SignalR test
packages and so on, which means **no backend code can be compiled, tested or run in that session**.
The .NET 10 SDK and Postgres 16 were installed and working; only package restore is blocked.

Consequences, decided deliberately rather than papered over:

- Phase 1 is frontend-only and fully verified (lint, build, 26 vitest tests, contrast script, screenshots).
- Phases 2 and 3 need new backend engines, migrations and tests. Writing roughly sixteen engines and
  migrations with no compiler, no test run and no database would break the "only push green commits" and
  "do not claim a result you did not run" rules, so none were written. Nothing is faked.
- To unblock: run this task again in an environment that can reach `api.nuget.org` (or give the session a
  NuGet mirror). Nothing in Phase 1 needs redoing; the game modules plug into the picker, categories, accents,
  shared `Scoreboard` and lazy-loading seam that Phase 1 built.

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
| Environment bootstrap | done (partial) | Backend restore blocked, see above |
| Phase 1: design overhaul | done | Wordmark hero + persistent header, stepped create flow, categorized picker, per-game accents, states, motion, a11y |
| Phase 2: shared primitives | partly | Frontend `Scoreboard`, category/accent registry and lazy loading done; backend round engine, timing, phase guards blocked |
| Would You Rather | blocked | needs backend engine (NuGet) |
| This or That | blocked | " |
| Most Likely To | blocked | " |
| Never Have I Ever | blocked | " |
| Family Feud-style | blocked | " |
| Trivia upgrades (categories, bank, time limit) | blocked | " |
| Two Truths and a Lie | blocked | " |
| Guess Who | blocked | " |
| Song/Movie intro guessing | blocked | " |
| Spin the Wheel | blocked | " |
| Buzzer | blocked | " |
| Bingo | blocked | " |
| Mad Libs | blocked | " |
| One-word story | blocked | " |
| Fortunately/Unfortunately | blocked | " |
| Codenames-style | blocked | " |
| Taboo-style | blocked | " |
| Pictionary-style | blocked | " |
| Full-diff review | done for Phase 1 | |

## Phase 1 verification

- `npm run lint`: 1 warning, the pre-existing one. `npm run build`: passes, JS 95.1 kB gzip (fonts self-hosted, latin subset 37 kB).
- `npx vitest run`: 26 tests pass (18 new: Scoreboard, ranking, player-count rules, GamePicker with 20 games, create flow).
- `npm run check:contrast`: 44 text and non-text pairings pass WCAG AA (script reads the real tokens from `index.css`).
- Screenshots: `docs/screenshots/`.
