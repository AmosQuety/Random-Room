# QA Report 2: The Playground (second full audit)

Audit date: 2026-10-03. Branch `feat/playground-redesign-and-games`. Method: scripted Playwright with real, isolated browser contexts per player; the application source was not modified, nothing was committed, `.env` was used as-is (only its signing key was read in-process for the token-expiry test, never printed). All scripts, traces, screenshots and logs are in the scratchpad directory outside the repo, so evidence paths below are relative to `/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/b34016c6-60b1-45e3-a3fb-03de919bcc68/scratchpad/`. PINs, tokens and invite tokens were redacted from everything saved.

## 1. Executive Summary

- **Games exercised with real players:** 19 of 19. Every game was played from room creation through the real UI, invite claiming on the real claim page, start, a full game, reveal, game over, and "Start new game". **4 simultaneous players per game minimum; 8 players for every game** (Chrome, Firefox and WebKit; Would You Rather also with 12 players). All 19 games allow 12; a 12-player run was done only for Would You Rather (burst and reconnect-storm test).
- **Runs:** 443 lifecycle runs (366 with 4 players, 77 with 8) across 22 browser/viewport/build combinations, plus about 60 targeted regression and probe scripts.
- **Browsers and viewports:** Chrome 152, Firefox 155 (Playwright build), Brave 152, WebKit 26.6 (desktop, iPhone 14 portrait and landscape, iPad), Chromium Pixel 7 portrait and landscape, iPad portrait and landscape, 320 px with reduced motion, 640 px (200% zoom equivalent), Firefox at 390 px, 768 px and 844x390. Production build: all 19 games on Chrome desktop, 7 on Pixel 7 emulation, plus Lighthouse, INP and cold slow-3G.
- **Findings:** Blocker 0, **Major 1**, Minor 7, Polish 7 (15 new).
- **Regressions of an earlier fix: 0.** All 8 earlier Major findings are fixed and confirmed in the browser. Five of the new findings are gaps in earlier fixes (R2-CONFIRM-01, R2-WORD-01/03, R2-SETUP-01, R2-BINGO-01, R2-CLAIMED-01); they are listed against the earlier ID in section 3.
- **Blocked / not tested:** spectator view (the app has no such role); real devices; screen readers; Brave tablet and Brave phone-landscape runs (stopped by agreement to spare the host machine, not a technical blocker); sweep, keyboard, offline and resilience scripts ran on Chrome only. See section 11.

**Top issues**
1. **R2-RACE-01 (Major).** When the host ends the game while a player's answer is in flight, every client stays on "LIVE" although the server says Completed (6 of 8 UI races; 12 of 12 protocol races). The answer's response snapshot has a higher sequence but stale session status, so clients discard the correct one. A reload fixes it.
2. **R2-CONFIRM-01 (Minor).** Bingo and the two story games still end the game for everyone with one click, and Bingo shows two different buttons both called "End game".
3. **R2-SETUP-01 (Minor).** Wheel, Bingo and Word Spies setup forms accept accent-only duplicates ("Cafe" with an accent and "cafe") that the server rejects after the last step.
4. **R2-WORD-02 / R2-BINGO-01 (Minor).** Small-phone reflow: Word Spies words split mid-word (21 of 25 at 320 px) and the Bingo called-item heading is clipped for long single words.
5. **R2-SKETCH-01 and R2-AUTH-01 (Minor).** Two-finger touch scribbles a zig-zag across the drawing; an expired token mid-game gives a generic error instead of the join screen.

## 2. Environment

- OS: Linux 7.0.0-30-generic. Node v22.22.1, .NET SDK 10.0.400, Playwright 1.63.0, axe-core 4.13.0, Lighthouse 13.5.0.
- Browsers: Google Chrome 152.0.7977.75, Brave 152.0.7977.76 (system builds); **Firefox 155.0 and WebKit 26.6 are Playwright's own builds**, not the system Firefox. WebKit launched and worked (checked first with a 5-line script).
- URLs: dev frontend http://localhost:5173, API http://localhost:5184, production build served by `vite preview` on :4173 from a scratchpad build (the repo's wwwroot was not touched; preview stopped afterwards).
- Database: PostgreSQL in Docker (`randomroom-pg`), host localhost, port 5433 (checked: host only). I rebuilt the API and restarted it by PID at the start; the running process might otherwise have predated the last backend commit.
- Rate limits: join 20 per minute (first 429 on request 21, with `Retry-After: 60`), create-room and claim 40 per minute. The pacing governor kept the harness to at most 3 rooms per minute and 14 joins per minute. 429s hit: 6 (all from harness bursts or deliberate tests, see `429.log`); none counted as findings.
- Environmental events: Docker Desktop was OOM-killed on the host at 15:34 (journal: oom-kill), taking Postgres down; I restarted it and re-ran the affected batch. No network flapping was seen this time.
- Harness corrections made during the audit (so earlier numbers were discarded, kept in `old/`): case-sensitive text checks against CSS-uppercase text, a stale-check that compared per-recipient clock sequences, Playwright's `setOffline` leaving WebSockets open (replaced by a routed socket that really drops), join-limit contention between parallel batches.

## 3. Part A: Regression table (earlier fixes re-tested through the UI)

| Earlier finding | Result | Evidence |
| --- | --- | --- |
| POLL-01, QUIZ-01, REFLEX-03, STORY-01, WORD-02 (stale screen) | **PASS** | 8-player runs, all players acting at the same instant: Would You Rather, Trivia (20 questions), Two Truths, Random Picker, Fill-in Stories, 3 repetitions each (Word Spies: 4 passing 8-player repetitions, 3 operatives clicking different cards at once). 172 rounds on Chrome 8p checked: 0 clients behind, host always had Next/Finish, final Trivia question showed "complete" without reload. Also 12 players in one burst and 3 reconnect storms (`results/burst12-chrome.json`). Note: pushes DO arrive out of order (up to 10 per client in a run) and the sequence number makes clients ignore them. |
| QUIZ-02 (Trivia leak) | **PASS** | `results/reg-quiz02-*.json`: DOM and frames of a not-yet-answered player have no activity/correct flag for the open question; it appears after the question closes. |
| REFLEX-01 (wheel) | **PASS** | `reg wheel`: 4 spins on Chrome, Firefox, WebKit sampled at 0.8, 1.4, 2.2, 3.0, 3.6, 4.6 s: result text, live region and host buttons hidden/disabled until the wheel stops; reduced motion shows the result promptly. Plus 3 spins in every lifecycle run. |
| REFLEX-02 (Bingo) | **PASS** for card cells (Chrome and WebKit, 390 and 320 px, no cell text outside its cell, no sideways scroll). **New related defect R2-BINGO-01** (called-item heading). | `runs/bingo-ovf-*` |
| POLL-02, QUIZ-06, REFLEX-06 (500s) | **PASS** | `reg limits` 20/20: 33/81/81/51/301 and 5000/100 give 400 naming the limit, exact limits accepted, inputs stop at the limit. |
| QUIZ-04, STORY-02, WORD-05 (page widens) | **PASS** | Width compared before/after reveal and game over, at lobby/live/reveal/game-over, in all 19 games on Pixel 7 (portrait and landscape), iPhone 14 WebKit (portrait and landscape): 4000 width checks, 0 failures. Still emulation only. |
| STORY-03 | **PASS** | 140 unbroken characters wrap on desktop, Pixel 7 and iPhone WebKit. |
| POLL-03, REFLEX-04, QUIZ-03 | **PASS** | Buzzer race loser sees no red alert, no stale alert next round; offline/online clears; timed Trivia end shows no "session is not active" and 0 conflicts. |
| WORD-01, WORD-11 | **PASS** | Both finishing modes: heading names the winner, sub-line names the right team, banner reads "Red team wins: A & B". |
| QUIZ-05 | **PASS** | "quiz", "story", "reflex", "poll", "word" list games on Chrome, Firefox, WebKit. |
| REFLEX-05, POLL-07, STORY-05, WORD-10 | **PASS** (picker/WYR/story/forbidden/sketch) with a gap: **R2-SETUP-01**. | `reg picker-and-setup` 14/14 on three engines. |
| WORD-09, POLL-10 (confirm) | **PASS** for the global End game and Word Spies End and reveal (asks first, focus on Keep playing, Escape and Keep playing cancel, keyboard-only works, double-click on Yes safe). **Gap: R2-CONFIRM-01.** | `reg confirm`, `confirm-wordspies`, `unconfirmed-ends` |
| POLL-06, QUIZ-08 (Round/Game) | **Partly**: header and status use Game N; leftovers **R2-WORD-01, R2-WORD-03**. | roundLines in `results/*.json` |
| POLL-04 | **PASS** | Back from the room lands on the home page (Chrome and WebKit iPhone). |
| POLL-05 | **PASS** with gap **R2-CLAIMED-01** | hint "Waiting for X, Y to open their invite link", "not joined" chips, late claim removes only that name. |
| WORD-03, WORD-04, WORD-08 | **PASS** | guesser ink grows during a 4 s drag (Chrome, Firefox, WebKit); at 200 strokes "The canvas is full" and drawer and guesser canvases match; right-click draws nothing. |
| WORD-06 | **PASS** | Canvas 273 px (Chrome 844x390), 252 (Pixel 7 landscape), 238 (iPhone WebKit landscape); rotation resizes and fits. |
| REFLEX-07 | **PASS (partly)** | Live-region text absent until the wheel stops (sampled over time). Not tried with a screen reader. |
| LEAD-02 | **PASS** | 429 on request 21 with `Retry-After: 60` and JSON body; join screen shows "Too many attempts. Wait a minute and try again." (`results/ratelimit.json`). See R2-JOIN-01 for the claim path. |
| POLL-08, WORD-12, POLL-09 (partial), QUIZ-07 (heading), STORY-04, POLL-10 (rank) | **PASS** with **R2-WORD-02** (words now readable at 390 but break mid-word below) and **R2-A11Y-01** (10 other games skip a heading level). | `reg ui-misc`, `ws-board`, `wsbreak.mjs` |
| B1 / join flow | **PASS** | Claim signs in and opens the room for every seat in every run; return via join with name + PIN works; join page names what is missing while disabled. |

## 4. Comparison with the first audit

- **Fixed and confirmed (browser):** all 8 Major findings; POLL-02/03/04/05/06 (mostly)/08/10, QUIZ-01..05, REFLEX-01..07, STORY-01..05, WORD-01..06, 08..12, LEAD-02.
- **Fixed but only partly confirmed:** POLL-06/QUIZ-08 (wording leftovers), WORD-09/POLL-10 (not every end button), POLL-07/WORD-10 (accent duplicates), REFLEX-02 (cells fine, called heading not), POLL-05 (turn screens), REFLEX-07 (no screen reader).
- **Regressed:** none.
- **New:** 15 (1 Major, 7 Minor, 7 Polish), section 5.
- **LEAD-01** (join LCP 2.8 s) stays not reproduced: 1.90 s on the production build.

## 5. Coverage matrix (4 players each unless noted)

PASS means the script ran, its output exists on disk and every check passed. PASS* means all runs that exist passed but not every engine/viewport in that column was run. Columns: Chrome/Firefox/Brave/WebKit = desktop 1280x800 dev server. Phone portrait = Pixel 7 (Chrome), iPhone 14 (WebKit), Firefox 390x844, Brave Pixel 7. Phone landscape = Pixel 7 landscape (Chrome), iPhone 14 landscape (WebKit), Firefox 844x390; **Brave landscape NOT TESTED** (stopped to spare the host). Tablet = iPad portrait and landscape (Chrome), iPad (WebKit), Firefox 768x1024; **Brave iPad NOT TESTED**. 8 players = Chrome, Firefox, WebKit. Prod build = Chrome desktop production build. All cells in the 8-player column used 8 simultaneous real players.

| Game | Chrome 4p | Firefox 4p | Brave 4p | WebKit 4p | phone portrait | phone landscape | tablet | 8 players | prod build | 320px+reduced motion | zoom200 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Random Picker | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Trivia | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Would You Rather | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| This or That | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Most Likely To | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Never Have I Ever | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Survey Showdown | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Two Truths and a Lie | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Guess Who Wrote It | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Name That Song or Movie | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Spin the Wheel | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Buzzer Round | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Bingo | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Fill-in Stories | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| One-Word Story | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Fortunately/Unfortunately | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Word Spies | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Forbidden Words | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |
| Sketch Guess | PASS | PASS | PASS | PASS | PASS | PASS* | PASS* | PASS | PASS | PASS | PASS |

Per-game simultaneous real players: **4 in every column except "8 players" (8)**. Every run also included the hidden-information scan of every player's WebSocket frames (426 scans, 0 leaks), axe at every phase (2000 scan checks) and page-width checks (4000).

## 6. Findings

## Major

### R2-RACE-01 (Major): all (shared GameSessionService/RoomScreen)
- **Category:** Bug (realtime/state)
- **Environment:** Chrome 1280x800 UI, 4 players; also protocol-level on the API
- **Steps:** 4 players in Would You Rather, game live. Host presses End game then Yes, end the game at the same instant another player presses an answer. Wait 4 s.
- **Expected:** Every client shows Game N complete (server says Completed); host sees Start new game.
- **Actual:** UI: 6 of 8 races left ALL four clients showing GAME N - LIVE while the server status was Completed; reload fixed it. Protocol: 12 of 12 races, the answer request returned 200 with a snapshot whose session.status=Active and a HIGHER sequence than the End response snapshot (Completed), and that stale snapshot was also pushed to everyone. Because clients keep the highest sequence (newerSnapshot) they discard the true Completed snapshot. Likely cause: the action response snapshot is built from the request-scoped DbContext whose tracked GameSession entity was loaded before End committed, so the higher-sequence build reads older state; it contradicts the sequencer comment that a higher sequence means fresher state. Checked the other direction too: a host Reveal racing an answer is ordered correctly (the older snapshot carries the lower sequence and clients ignore it), so the defect is specific to session-status changes (End game).
- **Impact:** Host and players see a live game that is over; answers get 409; host has no Start new game until reload. Reintroduces the first-audit stale-screen symptom for End game racing any action.
- **Evidence:** runs/end-race-chrome-desktop/ (checks.json, race*-stale-*.png, *.snaps.json showing Completed then Active with higher sequence), race-api.mjs output
- **Suggested direction:** Build action-response snapshots from fresh (AsNoTracking or new context) reads after the action commits, or re-read the session in BuildSnapshotAsync; add a server test where End and an action run concurrently.

## Minor

### R2-CONFIRM-01 (Minor): Bingo, One-Word Story, Fortunately/Unfortunately (shared story-chain screen)
- **Category:** UX issue (regression of fix WORD-09/POLL-10, incomplete)
- **Environment:** Chrome 1280x800, 3 players
- **Steps:** Host starts Bingo (or a story game) and presses the in-game host-bar button End game / End the story.
- **Expected:** Same confirm as the global End game: Yes, end the game / Keep playing.
- **Actual:** The game ends for everybody at once (Game over) with no confirmation. Bingo also shows two buttons both named End game (host bar: no confirm; Host controls: confirm).
- **Impact:** One mis-tap ends the group game; two identical labels with different behaviour confuse keyboard/screen-reader users.
- **Evidence:** results/reg-unconfirmed-ends-chrome-desktop-*.json
- **Suggested direction:** Route these buttons through ConfirmButton or remove them in favour of the shared End game; make labels distinct.

### R2-JOIN-01 (Minor): all (ClaimInviteScreen)
- **Category:** UX issue
- **Environment:** Chrome 1280x800
- **Steps:** Claim an invite and set a PIN while the per-IP join limit (20/min) is exhausted by other players on the same network.
- **Expected:** The claim succeeds but the screen tells the player that signing in is delayed and to wait a minute.
- **Actual:** The PIN is stored and the card says You are in, <name> with a Continue to the room button, although the automatic sign-in got HTTP 429 (see evidence). The player is not actually signed in.
- **Impact:** At a venue with one public IP, several rooms/players claiming within a minute produce a misleading success card; continue leads to the join form.
- **Evidence:** runs/word-spies-chrome-desktop-8p-zutj/error-wordsP5.png
- **Suggested direction:** Show the throttled message on the success card, or retry sign-in after Retry-After.

### R2-SETUP-01 (Minor): Spin the Wheel, Bingo, Word Spies (setup forms)
- **Category:** Bug (form does not mirror server rule)
- **Environment:** Chrome 1280x900
- **Steps:** In setup add two entries that differ only by accent: Cafe with acute accent and cafe (also Sun/sun is caught). Press Next, add players, Create the room.
- **Expected:** The setup step blocks Next with a specific message, like Sketch Guess and Forbidden Words do.
- **Actual:** The form accepts (it compares lowercase only). The server answers 400 Each wheel segment / bingo item / word must be different, shown only after the last step. Sketch Guess and Forbidden Words fold accents on both sides and agree. Additionally, when these three forms do block (Sun/sun) the sticky bar says only Finish the setup to continue; the specific sentence sits lower in the form. Random Picker treats Same and same as different choices on both sides (server 200), unlike the other games.
- **Impact:** Same late-error problem as POLL-07/WORD-10 for accented words (Luganda/Swahili content, e.g. words with diacritics).
- **Evidence:** results/parity.json, rev.mjs output
- **Suggested direction:** Share one normalise-and-compare helper (lib/text normalizeAnswer) with these forms; add setupIssue to these three modules.

### R2-WORD-02 (Minor): Word Spies (board.tsx), same pattern in Bingo cells
- **Category:** Responsive/UI + Accessibility (reflow, WCAG 1.4.4/1.4.10)
- **Environment:** Chrome, 4 players; widths 390, 360, 320 (= 400% zoom of 1280)
- **Steps:** Start Word Spies and open the board at 390, 360 and 320 px wide. Count board words that wrap onto two lines.
- **Expected:** Words stay whole and readable (smaller type, fewer columns, or a scrollable board).
- **Actual:** 5 of 25 words split mid-word at 390 px (meadow, sandwich, window, snowman, compass), 12 of 25 at 360, 21 of 25 at 320 / 400% zoom (e.g. islan/d, turtl/e, light/hou/se). Cause: overflow-wrap:anywhere in a 5-column grid. Nothing is clipped and there is no sideways scroll (WORD-12 clipping fixed), but guessers must read word fragments.
- **Impact:** A word-guessing game whose clues are the words becomes hard to read on small phones and at 400% zoom.
- **Evidence:** wsbreak.mjs output; runs/wsbreak-chrome-desktop/wsbreak-320-*.png; runs/sweep-word-spies-chrome/word-spies-chrome-320x200-live-sweepP1.png
- **Suggested direction:** Use 3 or 4 columns below sm, or smaller text with hyphens:auto instead of anywhere.

### R2-AUTH-01 (Minor): all (RoomScreen action path)
- **Category:** UX issue / error handling
- **Environment:** Chrome 1280x800, 3 players, Random Picker
- **Steps:** Sign in, load the room, let the token pass its expiry (a correctly signed token with a near expiry was made in-process, never printed; the API grants 60 s clock skew) and press an action button.
- **Expected:** The player is told the session ended and is taken to the join screen (as happens when the room is loaded with an expired token).
- **Actual:** POST /api/room/action returns 401; the screen stays on the room, the connection pill still says LIVE, and a red alert says only Something went wrong. Every further press repeats it. Loading the room with an expired token is handled correctly (returns to the join screen and clears storage).
- **Impact:** After a 12-hour token expires (all-day event, tab left open) the player is stuck with a generic error until they reload.
- **Evidence:** midexp.mjs output; runs/midexp/midexp-midexP3.png; results/resil-expiry.json
- **Suggested direction:** Treat a 401 from an action like the 401 on refetch: clear the session and show the join screen with a plain message.

### R2-SKETCH-01 (Minor): Sketch Guess (Board.tsx pointer handling)
- **Category:** Bug (touch input)
- **Environment:** Chromium, Pixel 7 emulation, CDP touch events
- **Steps:** As the drawer put two fingers on the canvas at the same time (or rest a palm while drawing) and move both for about a second.
- **Expected:** One finger draws; extra touches are ignored, or each touch makes its own stroke.
- **Actual:** The two touches are drawn as ONE stroke whose points alternate between the two fingers: 40 points in 2 strokes with 37 long jumps between consecutive points (a zig-zag across the board) that every guesser also receives. Cause: a single current-stroke ref is shared by all pointers and pointerdown of the second finger replaces it. One-finger drawing is fine and the page did not scroll or zoom.
- **Impact:** Resting a palm or a second finger on the canvas of a phone or tablet scribbles a streak across the drawing, and it can only be removed with Undo.
- **Evidence:** results/sketch2-twofinger-chrome.json
- **Suggested direction:** Track the active pointerId and ignore pointermove/pointerdown from other pointers (isPrimary).

### R2-BINGO-01 (Minor): Bingo (called-item heading; related to REFLEX-02)
- **Category:** Responsive/UI
- **Environment:** Chrome and WebKit (bingo-ovf), 320 px wide (also 390 for items of 25+ characters)
- **Steps:** Create Bingo with custom items such as Supercalifragilistic12 (22 characters, no spaces), start, host calls the first item, open a player at 320 px.
- **Expected:** The called item is fully readable (wraps, or shrinks), as the card cells now do.
- **Actual:** The large called-item heading does not wrap: it shows Supercalifragil... (Chrome) or Antidisestablis... (WebKit) cut off at the card edge. Card cells (the REFLEX-02 fix) wrap correctly; the page does not scroll sideways.
- **Impact:** Players cannot read the item that was just called, which is the core information of the game, when an item is a long single word (custom items allow 40 characters).
- **Evidence:** runs/bingo-ovf-chrome-desktop/bingo-chrome-320x800-long-items-bingoP2.png, runs/bingo-ovf-webkit-desktop/bingo-webkit-320x800-long-items-bingoP2.png
- **Suggested direction:** Add overflow-wrap:anywhere and a smaller size for long items on the called-item heading (bingo/GameScreen.tsx).

## Polish

### R2-WORD-01 (Polish): all round games (RoundGameScreen) and Random Picker; create screen
- **Category:** Content/copy issue (leftover Round/Game wording)
- **Environment:** Chrome 1280x800
- **Steps:** Create any prompt-and-answer game (Would You Rather, Trivia-like round games). Before the host starts, read the waiting card as the host. Also finish Random Picker and read the Final result heading and activity list.
- **Expected:** Game = whole play-through, round only for prompts (POLL-06 fix).
- **Actual:** Waiting card tells the host Press Start round below when everyone is here, but the button is Start game. Random Picker shows Final result - round N and Activity entries - round N where N is the game number. The create screen hint says The host starts and ends rounds.
- **Impact:** Instruction names a button that does not exist; two meanings of round remain.
- **Evidence:** results/*would-you-rather-chrome-desktop*.json roundLines; source RoundGameScreen.tsx:71, random-picker/FinalResult.tsx:20, ActivityTimeline.tsx:20, CreateRoomScreen.tsx host hint
- **Suggested direction:** Say Press Start game below; use Game N in Random Picker; reword the host hint.

### R2-A11Y-01 (Polish): 10 of 19: Trivia, Most Likely To, Never Have I Ever, Survey Showdown, Guess Who, Name That, Bingo, Fill-in Stories, Word Spies (and any game using the same card heading)
- **Category:** Accessibility (heading order, WCAG 1.3.1 best practice)
- **Environment:** Chrome 1100x800 live screen; axe best-practice rule heading-order and Lighthouse accessibility audit (98 instead of 100 on Trivia, Bingo, Word Spies)
- **Steps:** Start any of the listed games and read the heading outline of a player.
- **Expected:** h1 followed by h2 (the fix QUIZ-07 gave Two Truths).
- **Actual:** h1 then h3 (question / prompt / Your card / Write your fact), then h2 Scoreboard. Games that do not use a heading for the prompt (Would You Rather, Buzzer, Wheel, Random Picker, story games, Forbidden Words, Sketch Guess) are fine.
- **Impact:** Screen-reader heading navigation skips a level; Lighthouse accessibility drops to 98.
- **Evidence:** results/headings.json; results/old/lh-inroom-a11y.json
- **Suggested direction:** Use h2 for the card headings (RoundGameScreen prompt, Trivia question, Bingo card, Word Spies status, Guess Who facts) as done for Two Truths.

### R2-A11Y-02 (Polish): all (header logo link); Word Spies spymaster board
- **Category:** Accessibility (label in name, WCAG 2.5.3)
- **Environment:** Lighthouse 13 on the production build, mobile preset
- **Steps:** Run Lighthouse accessibility on any in-room page.
- **Expected:** Accessible name contains the visible text.
- **Actual:** label-content-name-mismatch: the logo link has aria-label The Playground home but its visible text is rendered as THE and Playground in separate spans; two Word Spies spymaster cards (aria-label like meadow, Blue (secret)) differ from the visible word plus owner text.
- **Impact:** Voice-control users saying the visible text may not activate the link; minor.
- **Evidence:** results/old/lh-inroom-a11y.json
- **Suggested direction:** Start the aria-label with the visible words (The Playground ...) or drop the label; keep the visible card text first in the card label.

### R2-SKETCH-02 (Polish): Sketch Guess
- **Category:** UX (behaviour change from the segmenting fix)
- **Environment:** Chrome 1100x1000, continuous mouse drag
- **Steps:** Drag continuously for 60 s on the drawer canvas (round set to 180 s) and read the stroke count on the server.
- **Expected:** Cap of 200 strokes reached only by a very detailed drawing.
- **Actual:** 151 strokes after 60 s of non-stop drawing (about 2.5 strokes/s, the 500 ms segmenting), so the 200-stroke cap is reached after about 79 s of non-stop drawing. Tapped dots and many short strokes still hit it first (see WORD-04).
- **Impact:** Rarely reached in real play; a child filling a large area with a marker could see The canvas is full before the round ends.
- **Evidence:** results/sketch2-long-chrome.json
- **Suggested direction:** Count segments of one gesture as one stroke for the cap, or raise the cap to about 400.

### R2-LOAD-01 (Polish): all (index.html)
- **Category:** UX / Accessibility (no JavaScript, failed script load)
- **Environment:** Chrome with JavaScript disabled, dev and production build
- **Steps:** Open the site with JavaScript off, or with the script blocked by a proxy or content filter.
- **Expected:** A short explanation that the app needs JavaScript (a noscript element), or a visible failure after a timeout.
- **Actual:** The static line Loading The Playground... stays on screen forever (no noscript element, no timeout), which reads as an endless load. With JavaScript on there is no duplicate text, no flash and CLS 0.0002 (prod) when React mounts.
- **Impact:** On networks that drop the script (the target low-bandwidth audience) the user waits with no hint.
- **Evidence:** nojs.mjs output
- **Suggested direction:** Add a noscript message and a small inline script that changes the line to a retry hint after about 15 s.

### R2-WORD-03 (Polish): all (API error text shown in the red alert)
- **Category:** Content/copy issue (vocabulary)
- **Environment:** API messages, seen in the UI alert after a refused action
- **Steps:** Press an answer after the host ended the game (a 409), or read GameSessionService/RoomRuleException messages.
- **Expected:** Same word as the screens: game.
- **Actual:** Players and hosts are told: The session is not active. / Only an active session can be ended. / Only a waiting session can be started. / Only the host can control the session. / End the current session before starting a new one. The screens say Game N, Start game, End game, Start new game.
- **Impact:** Two words (session, game) for one thing; the 409 text reaches players.
- **Evidence:** backend GameSessionService.cs messages; reg errors test
- **Suggested direction:** Reword the five messages to game.

### R2-CLAIMED-01 (Polish): Two Truths (observed); turn games by source: story games, Spin the Wheel, Sketch Guess, Forbidden Words
- **Category:** UX issue (gap in the POLL-05 fix)
- **Environment:** Chrome 1280x800, 4 seats, 3 claimed
- **Steps:** Two Truths and a Lie: start with one invite unclaimed; reach the round whose storyteller is the unclaimed player.
- **Expected:** Players and host are told the storyteller has not opened their invite link (as the voting screen does).
- **Actual:** Voting screens do say Waiting for X to open their invite link and show a not joined chip, and the host gets Skip. But on the unclaimed storyteller turn the card says only Waiting for <name> to write two truths and a lie, with no hint that the seat is unclaimed. Same wording pattern in the story, wheel, drawing and Forbidden Words screens by source (they only use the claimed flag in the shared ProgressChips). Also, with unclaimed seats the reveal never happens by itself, the host must press Reveal now.
- **Impact:** The group waits for someone who has not joined; the host may not realise Skip is the answer.
- **Evidence:** results/reg-tt-claimed-chrome-desktop-*.json
- **Suggested direction:** Use the claimed flag in the shared waiting-for-a-player message.


### Observations that are not findings
- Dev server only: layout shift of 0.15 to 0.27 on a throttled reload (`results_dev_net/`) and the LEAD-03 console message. Neither appears in the production build (CLS 0.005 to 0.056 on slow 3G, no connection error in 19 production runs).
- The `sequence` is the server clock in milliseconds, assigned per recipient build: it leaks only server time, not any game data or identifier.
- Random Picker treats "Same" and "same" as two choices on both form and server; the other games fold case. Consistent between form and server, but a different rule.
- Game tiles in the picker change their accessible name to start with "Selected" once chosen (first noted under QUIZ-07). Still present.

## 7. Cross-game patterns

1. **Shared room snapshot path (R2-RACE-01).** `GameSessionService` builds action-response snapshots from the request's tracked entities; any session-status change racing an action can produce a newer-numbered but older snapshot. Pure game-state races (answer, reveal, vote) are correct.
2. **End controls are not all routed through the shared confirm** (R2-CONFIRM-01): `ConfirmButton` is used by HostControls and Word Spies only.
3. **Setup rules are mirrored per game, not shared** (R2-SETUP-01): Sketch and Forbidden fold accents, Wheel/Bingo/Word Spies compare lowercase only.
4. **Reflow at 320 px**: `overflow-wrap:anywhere` is used in grids (R2-WORD-02) while headings have none (R2-BINGO-01).
5. **Card headings are h3 under h1** in the round screens (R2-A11Y-01).
6. **Leftover vocabulary** in RoundGameScreen, Random Picker, CreateRoomScreen and the API messages (R2-WORD-01/03).

## 8. Accessibility

- **axe-core (WCAG 2 A/AA/2.1/2.2 tags): 4184 scans in 453 runs.** No stable violation in the final harness. Two single color-contrast hits in the very first pass (before the settle re-scan existed) were never reproduced; 72 first scans flagged color-contrast while an enter animation was still running and every one cleared on the re-scan 1.5 s later (not stable defects).
- axe best-practice and Lighthouse: heading-order on 10 games (R2-A11Y-01), label-content-name-mismatch (R2-A11Y-02). Lighthouse accessibility 98 to 100.
- `npm run check:contrast`: all 44 pairings pass.
- **Keyboard-only** (player 2 keyboard only, others mouse): Would You Rather, Trivia, Bingo, Buzzer, Spin the Wheel, Fortunately, Forbidden Words, Word Spies, Sketch Guess, Fill-in Stories: every focus stop had a visible indicator, no trap, order follows the visual order, Escape closes the confirm and focus returns. Limits: roles are random, so in Wheel, Forbidden, Word Spies and Sketch the keyboard player did not always hold an acting role that round (the script records which); Sketch drawing has no keyboard equivalent (known).
- **Reduced motion:** all 19 games at 320 px pass; wheel result is prompt. **Reflow:** 320 px and 640 px (200%) lifecycle runs for all 19 pass with axe; the width sweep (1920, 1440, 1280, 1024, 768, 360, 320, plus 640x400 and 320x200 as 200% and 400% equivalents) passed 56 of 56 checks in each of 6 games (Would You Rather, Trivia, Bingo, Fortunately, Word Spies, Sketch Guess) in lobby, live and over states; live resize 1920 to 320 during play kept state and typed drafts. Zoom is emulated by viewport size, not real browser zoom.
- **Confirm control:** group named "End the game for everyone?", focus lands on Keep playing, Escape cancels.
- **Could not test:** screen-reader output (no screen reader available; live-region text inspected instead), OS large text.

## 9. Performance (production build, Lighthouse 13.5 mobile preset, simulated throttling, cold cache, median of 3)

| Page | Perf | LCP | FCP | CLS | TBT | JS transferred |
| --- | --- | --- | --- | --- | --- | --- |
| Home | 99 | 1.73 s | 0.98 s | 0.0003 | 95 ms | 101.8 KB |
| Game picker / create screen | 99 | 1.72 s | 0.99 s | 0.0003 | 33 ms | 101.8 KB |
| Join page | 99 | 1.90 s | 1.51 s | 0 | 6 ms | 101.8 KB |
| In room: Would You Rather (poll) | 99 | 2.13 s | 0.99 s | 0.013 | 24 ms | 110.5 KB |
| In room: Trivia (quiz) | 99 | 2.13 s | 0.96 s | 0.048 | 15 ms | 106.7 KB |
| In room: Bingo (reflex) | 98 | 2.13 s | 0.98 s | 0.070 | 13 ms | 106.2 KB |
| In room: Word Spies (word) | 98 | 2.14 s | 0.99 s | 0.067 | 49 ms | 107.2 KB |
| In room: Fortunately (story) | 99 | 2.12 s | 0.99 s | 0.016 | 26 ms | 106.1 KB |

All within the targets (LCP < 2.5 s, CLS < 0.1, JS < 150 KB); in-room CLS reaches 0.07, the highest figure. Baseline comparison: home 1.7 s, join 1.9 s, JS 102 KB: unchanged. Main bundle 350.6 kB raw, 103.6 kB gzip.

**INP** (real interaction traces, Pixel 7 emulation, 4x CPU slowdown, production build): create flow 128 ms (38 interactions), Would You Rather 64 ms, Survey Showdown typing 88 ms, Trivia 64 ms, Bingo 136 ms, Fortunately typing 56 ms. All under 200 ms.

**Slow 3G (400 kbps, 400 ms), production, cold cache, Pixel 7:** loading line 0.5 s, skeleton 3.3 s, heading 3.8 s, game UI 4.3 to 4.9 s, 153 to 158 KB, CLS 0.017 to 0.054 (Would You Rather, Trivia, Bingo, Word Spies, Sketch Guess). Fast 3G: game UI 1.5 s. Dev server, reload with warm cache, all 19 games: game UI 9.4 s and layout shift 0.15 to 0.27 (dev only). Offline, all 19 games (real socket drop): reconnecting notice shown, action while offline gives a plain error where the game has an immediate action, back online reconnects without reload, stale error cleared, retry records exactly once, a player offline while the host ends the game catches up. 19 of 19 pass.

**Long session:** 30 Trivia questions, heap after forced GC 7.7 MB to 9.3 MB then flat; DOM nodes plateau near 900 (`results/heap.json`).

## 10. Security and hidden information

**Confirmed issue:** none that exposes data. R2-RACE-01 and R2-AUTH-01 are integrity/UX defects.

**Tested and passed**
- Frames of every player in every one of 426 lifecycle runs, and DOM per role (host, player, spymaster/operative, describer/judge/guesser, drawer/guesser, storyteller/voter): Word Spies key, Forbidden Words card, Sketch word, Bingo cards (own only, different per player), Fill-in results, Two Truths lie, Guess Who author, Trivia correct answer and activity, Name That accepted answers, poll results before reveal.
- Not-yet-joined seat / anonymous visitor: the room preview for all 19 game types contains no game content and no invite token (random marker strings in every game's custom content); it does list player names and whether each is claimed (by design).
- Non-host `start`, `end`, `new` session: 403; non-host `reveal`: refused; no token or garbage token: 401; SignalR negotiate without token refused; out-of-phase actions 409; answering twice 409; invalid answer 400; unknown action 4xx with no stack trace; an `actor`/`player` field in the body cannot act as someone else; 2 MB body no 500; replaying a used invite refused; unknown game type 400 problem+json.
- Token: expired token 401 (UI returns to join and clears storage); signing proof via control token. The API grants 1 minute of clock skew (Program.cs).
- PIN is not in storage, URL, DOM or logs; the session token is in localStorage and the SignalR `access_token` is in the WebSocket URL (known, normal).
- Races: Buzzer races in every buzzer run gave exactly one winner; simultaneous Bingo claims resolved to one result.

**Could not test:** forged valid tokens for other players (by design), penetration-style testing.

## 11. Not tested / blocked

| Area | Status and reason |
| --- | --- |
| Spectator view | BLOCKED: the app has no spectator role. |
| Brave iPad and Brave phone landscape | NOT TESTED: stopped to spare the host machine at the user's request; not a technical blocker. |
| Real devices, iOS Safari, keyboard covering inputs, safe areas, browser chrome height | Not testable with emulation. "Page widens" and landscape findings pass in Chromium and iPhone WebKit emulation; real-device behaviour is **unverified**. |
| Screen reader, OS large text, phone lock/background freeze | No screen reader or OS-level control available; CDP freeze not attempted. |
| Width sweep (6 of 19 games), keyboard-only (10 of 19), offline/resilience scripts, Lighthouse | Chrome only (WebKit and Firefox have no CDP throttling; Lighthouse needs Chrome). |
| Slow-3G production run | 5 games (Would You Rather, Trivia, Bingo, Word Spies, Sketch Guess); the other 14 ran on the dev server only. |
| Refresh in every phase | 5 games (Would You Rather, Trivia, Bingo, Fortunately, Word Spies/Sketch), not all 19. |
| 12-player run | Would You Rather only. |
| README `npm run build -- --outDir` claim | Not run: `tsc -b` would write build info inside the repo. |
| Gaps_Bugs stale server | Unable to verify beyond restarting the API myself. |

## 12. Known-open items (not re-reported)

| Item | Status |
| --- | --- |
| G1 invite improvements, G2 no PIN recovery | Unchanged, design decisions pending. |
| QUIZ-06 remainder | Still present: 40 options, a 3000-character option and 300 questions are all accepted (200). |
| POLL-09 built-in content | Not re-reviewed; duplicated lead-in is fixed (once in the round card). |
| QUIZ-07 remainder | Three `role=status` regions still on the Two Truths screen; focus after voting not re-measured. |
| WORD-07 Sketch canvas below the fold on a laptop | Still present by observation (the drawer's page scrolls to reach canvas and tools); not re-measured. |
| LEAD-03 dev console message | Seen in dev (4 to 8 per run); not seen in the production build. |
| Sketch Guess has no keyboard drawing | Still present. |
| Buzzer fairness (arrival order) | Still present; every race had exactly one winner. |
| Migrations run on startup | Still present: `Program.cs:129` migrates unconditionally. |
| Leftover drawing strokes | Still present: after the host ended a Sketch game mid-drawing, the Completed session still holds its 4 strokes in `GameSessionStates.DataJson`. |
| No spectator role; no category filter in the picker | Unchanged. |

## 13. Self-audit

**Covered:** everything in sections 1 to 12.

**Not covered or weaker:** see section 11. Specific weaknesses: the keyboard run's player role is random per game; the "all clients revealed" check is DOM-based (sr-only text) and the "behind the server" check compares each client's last pushed phase and round with the server's; several harness checks first passed vacuously (uppercase text) and were corrected, so earlier outputs were discarded; some Playwright interactions needed the label rather than the hidden radio input in Firefox at 768 px (a harness issue, not an app defect).

**Server restart (lead, last):** with 2 of 4 answers in, the API was stopped and restarted (6.3 s back). All four clients still showed "2 of 4 answered" and LIVE. After the last two answered, only 1 of 4 clients showed the results within 4 s, but **all four showed them by 14 s**, with no duplicate players and 4 answers recorded. So the first audit's observation reproduces as a **delay of several seconds after a restart (SignalR reconnect back-off) that heals by itself, not as a permanent stale screen.** I did not treat it as a finding.

**Assumptions:** the dev server stands for the app's behaviour except where the production build was measured (section 9). Emulated phones are not real phones. Zoom is emulated by viewport size. Firefox and WebKit are Playwright builds.

**Environmental:** one Docker Desktop OOM kill (host), no network flapping seen; timing numbers from the runs overlapping the outage were re-run.
