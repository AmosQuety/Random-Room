# Progress

## Summary (updated at the end of each phase)

**Status: Phase 1 (design overhaul) done. Phase 2 (shared primitives) done. Phase 3 (games) in progress: see the checklist.**

Run 1 could not build the backend (NuGet was unreachable). Run 2 has NuGet, so the backend compiles and its tests
run against a real Postgres. Every game below is a full plugin: backend engine, frontend module, tests on both sides.

Open item for the owner: the three run-1 commits are authored as the tool's identity, not as `AmosQuety`. Rewriting
them needs a history rewrite and a force-push (`git rebase -r --exec 'git commit --amend --no-edit --reset-author'`,
then `git push --force-with-lease`), which this session was not permitted to run. Every commit from run 2 onward is
authored as `AmosQuety`.

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
