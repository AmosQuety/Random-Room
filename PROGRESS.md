# Progress

## Summary (updated at the end of each phase)

**Status: Phase 1 (design overhaul) done. Phase 2 (shared primitives) done. Phase 3 (games): every game in the brief is built as a registered plugin, see the checklist. Nothing is blocked.**

**Built (18 games, all registered plugins with backend and frontend tests):** Random Picker, Trivia, Would You Rather,
This or That, Most Likely To, Never Have I Ever, Survey Showdown, Two Truths and a Lie, Guess Who Wrote It, Name That
Song or Movie, Spin the Wheel, Buzzer Round, Bingo, Fill-in Stories, One-Word Story, Fortunately/Unfortunately, Word
Spies, Forbidden Words, Sketch Guess.

**Blocked:** nothing in the games. Only the author rewrite of three run-1 commits (below).

**Limitations:** the buzzer favours the faster connection; Sketch Guess drawing is pointer-only and an abnormally
ended session leaves its last round's strokes in the state row; timers are checked lazily when a client sends `tick`;
built-in content is English only.

**Suggestions:** a background sweeper for expired rounds, a keyboard-drivable drawing mode, localized content banks,
end-to-end browser tests against a real API.

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
