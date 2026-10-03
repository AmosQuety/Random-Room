# QA TASK 2: Second full audit of "The Playground" (regression + gap coverage)

You are a **senior QA engineer, browser automation engineer and UI/UX reviewer**. This is the **second** end-to-end audit of the app in:

`/home/amos/dev/dotnet/Choice_Maker` (branch `feat/playground-redesign-and-games`)

You have no memory of earlier work. Everything you need is in this file and the files it points to. Read it fully before touching anything.

## 0. How this prompt relates to the first one

* **`docs/QA_PROMPT.md` is the full method** (rules, harness, role views, hidden-information checks, severity levels, evidence format, report structure). Read it completely. **Everything in it still applies**, except where this file overrides it.
* **This file adds:** what changed since the first audit, a regression list, the coverage gaps to close, the environment that now exists, and traps the first run fell into.
* **Baseline:** `QA_REPORT.md` is the first audit. **Section 14 ("Status after fixes")** lists each finding's status and the commit that fixed it. Read the whole report.
* `Gaps_Bugs.md` lists known gaps. Do **not** report anything listed there as new (see section 7 of this file).

### Overrides to `docs/QA_PROMPT.md`

1. The report goes to **`QA_REPORT_2.md`** in the repo root. That is the only file you may create in the repo. Do not edit `QA_REPORT.md`.
2. Finding IDs in your report are prefixed **`R2-`** (for example `R2-POLL-01`), so they cannot be confused with first-audit IDs.
3. Section 28 of the first prompt (player identities) is unchanged: there are no fixed identities; players are created per room through the invite link and choose their own PIN.
4. The first prompt says WebKit may be blocked. **It is not blocked any more** (see section 2).

Rules that matter most, repeated: **do not modify application source code, do not fix bugs, do not change configuration to make a test pass, do not commit, never print `.env`, never claim PASS without having exercised the behaviour, "NOT TESTED / BLOCKED" needs a technical reason (never cost or time), and cost is never a reason to reduce coverage.**

## 1. What changed since the first audit

About 40 commits fixed 8 Major, most Minor and many Polish findings. Facts a tester must know so the harness and expectations are right.

**Realtime**
* Every room snapshot now carries a `sequence` number (per room, assigned while building under a per-room lock). Clients keep the highest and ignore older ones. This fixed the "stale screen after simultaneous actions" bug.
* `PlayerView` now has `claimed`. Round games show who has not opened their invite link.
* The API answers a throttled request with HTTP 429, a `Retry-After` header and a problem+json body.

**Wording and controls the harness must use (selectors changed)**
* The room-level unit is a **"game"**, not a "round". Header pill: `Game 1 · Live`. Buttons: **`Start game`**, **`End game`**, **`Start new game`**. "Round N of M" is only for prompts inside a game.
* **`End game` asks first.** Pressing it shows "End the game for everyone?" with **`Yes, end the game`** and **`Keep playing`** (focus lands on Keep playing; Escape cancels). Word Spies has its own button, **`End and reveal`**, with **`Yes, end and reveal`**.
* After a game ends the host sees `Start new game` and the note "Scores start again from zero."
* Setting a PIN on the claim page now **signs the player in and opens `/room/<slug>`** (no extra join step). Back from the room goes to the page before the invite, not to the used invite.
* Fortunately/Unfortunately: the label is "Finish the sentence" and the lead-in ("Fortunately,") sits beside the input.
* Scoreboards show `-` (and "Not ranked yet" to screen readers) while everyone is on zero.
* The game picker search also matches category names ("quiz", "story").
* The setup step shows a specific message when it blocks "Next" (for example "Each card needs a different word.").
* Spin the Wheel keeps the result hidden (and not announced) until the wheel stops, for every spin.
* Word Spies "all words found" names the team that found them; a team win is announced as "Red team wins: A & B", not as a tie.
* Survey Showdown rows no longer show a percentage.
* The page has a static "Loading The Playground..." line before the script loads.

**Limits now enforced with a clear 400 (and matching form `maxlength`)**: room name 80, player name 32, Random Picker choice 80 and at most 50 choices, Trivia question text 300.

## 2. Environment (already prepared; reuse it)

* **Services are normally already running. Check before starting anything:**
  * PostgreSQL: Docker container `randomroom-pg`, port `5433`.
  * API: `:5184`. Find it with `ss -ltnp | grep :5184`. It must be running the **current** build. If you are unsure, `cd backend/RandomRoom.Api && dotnet build`, stop the old process **by PID**, and start `dotnet run --no-launch-profile --no-build --urls http://localhost:5184` in the background. Restarting to run current code is allowed; changing config is not.
  * Frontend dev server: `:5173` (`cd frontend && npm run dev`), proxying `/api` and `/hubs` to the API.
* The local `backend/RandomRoom.Api/.env` exists. **Never print, quote, grep or copy it.** It contains one commented-out line that looks like a production database URL: do not read it out, and confirm only that the *active* `ConnectionStrings__Default` host is `localhost` on port `5433` (check the host only). Abort if it is anything else.
* **WebKit works now** (system libraries installed). Check with a 5-line launch script before relying on it. `libWPEWebKit` showing as missing in `ldd` is irrelevant. Use `devices["iPhone 14"]`, `devices["iPhone SE"]`, `devices["iPad (gen 7)"]` and friends for WebKit phone/tablet runs.
* Browsers: Chrome `/usr/bin/google-chrome`, Firefox `/usr/bin/firefox`, Brave `/usr/bin/brave-browser`, Playwright WebKit.
* Put **all** scripts, traces, screenshots and installed tooling (`playwright`, `@axe-core/playwright`, `lighthouse`, `chrome-launcher`) in **your own scratchpad directory outside the repo**.
* **Production build:** never run a bare `npm run build`; it empties `backend/RandomRoom.Api/wwwroot`. Build to your scratchpad: `npx vite build --outDir <scratch>/dist --emptyOutDir`, then `npx vite preview --outDir <scratch>/dist --port 4173` (preview inherits the `/api` proxy). Stop the preview server when you finish.

### Rate limits (the first run's biggest source of noise)

All browser contexts share one IP, so the limits are one budget: **`POST /api/join` 20 per minute; create-room and claim-invite 40 per minute.** One room costs about 5 admin calls and 4 joins. Build the pacing governor from the first prompt: at most **3 rooms per minute**, wait out a `429` using its `Retry-After`, record every 429 you hit. A 429 caused by harness speed is not a finding. Run **at most 2 agents at once** if you use subagents, and run server-restart tests only after everyone else has finished.

## 3. Traps the first run fell into (avoid them)

* **Never use `pkill -f <pattern>`**: the pattern matches your own shell command line and kills your session. Find PIDs with `ss -ltnp` or `pgrep -x` and `kill <pid>`.
* **Playwright strict mode:** `getByText(/Round 1 of/)` matches two elements (a visible heading and a screen-reader `status` paragraph). Use a specific locator such as `#round-heading` or `.first()`.
* Old scripts look for the buttons `Start round` / `End round` / `Start new round`. They are now `Start game` / `End game` / `Start new game`. A timeout waiting for "Start round" is the rename, not a regression.
* If the network on the host flaps (the first run saw it every ~30 s), **say so in the report** and re-run affected timing/resilience tests once it is stable. Do **not** work around it with a private network namespace or by stubbing the dev server's HMR socket.
* Mobile "page widens after reveal" must be checked in **both** Chromium mobile emulation and WebKit iPhone emulation. Report real-device behaviour as unverified; do not claim it.
* Playwright `isEnabled()`/`isVisible()` do not wait. Poll for the spinner/host rather than checking once.
* A passing unit test is not a pass for you. You test through the browser.

## 4. Part A: Regression (every earlier fix, re-tested through the UI)

For each row: re-run the original reproduction through real browser players, record PASS/FAIL, and keep evidence. A FAIL here is a **regression** and gets a high-priority finding with the earlier ID in its title.

| Earlier finding | Re-test | Pass means |
| --- | --- | --- |
| POLL-01, QUIZ-01, REFLEX-03, STORY-01, WORD-02 (stale screen) | **8 players, all act at the same instant**, in each of: Would You Rather, Trivia (20 questions), Two Truths (votes), Random Picker (all trigger), Fill-in Stories (all lock in), Word Spies (3 operatives click different cards at once). Repeat each at least 3 times. After 4 s compare every client with the server state (and with its own WebSocket frame log) | 0 clients behind the others; host always has Next/Finish when the round is over; final Trivia question shows "complete" without a reload |
| QUIZ-02 (Trivia leak) | While a question is open, inspect another player's DOM **and** SignalR frames | No `correct` flag for the open question anywhere; activity appears only after it closes |
| REFLEX-01 (wheel) | Spin 3+ times; at 0.8 s after each click check the result banner, the host's Award/Next, and the status live region | Hidden/disabled/empty until the wheel stops, for **every** spin; reduced motion still correct |
| REFLEX-02 (Bingo) | Items like `Supercalifragilistic12`, phone 390 and 320 wide | Every cell's text inside its cell, no horizontal scroll |
| POLL-02, QUIZ-06, REFLEX-06 (500s) | `POST /api/rooms` and the UI with name 33 chars, title 81, picker choice 81, 51 choices, trivia question 301, plus values exactly at each limit | Over: HTTP 400 with the limit named, never 500; at the limit: accepted; form inputs stop typing at the limit |
| QUIZ-04, STORY-02, WORD-05 (page widens) | Reach a reveal and a Game over on a phone in Chromium mobile **and WebKit iPhone**; read `innerWidth`/`scrollWidth` before and after | Width unchanged (390 stays 390) |
| STORY-03 | 140 unbroken characters in Fortunately | Wraps inside the card on desktop and phone |
| POLL-03, REFLEX-04, QUIZ-03 | Lose a Buzzer race; answer after the round closed; go offline then online; let a timed Trivia game end | Error text disappears when a newer state arrives; no "session is not active" alert at the end; buttons never lock because of a timer tick |
| WORD-01, WORD-11 | Finish Word Spies by "all found" and by assassin | Correct team named in the sub-line; team win banner is "<Team> team wins: names", not "tie" |
| QUIZ-05 | Picker search "quiz", "story", "reflex" | Matching games listed |
| REFLEX-05, POLL-07, STORY-05, WORD-10 | Setup with duplicate picker choices, half-written dilemma, no story starter, duplicate Forbidden Words cards, a Sketch word "!!!" | "Next" blocked on the setup step with a specific message; no error only after Create |
| WORD-09, POLL-10 | Host presses End game (and Word Spies End and reveal) | A confirm appears first; Keep playing and Escape cancel; confirming ends the game; keyboard-only works; focus is on Keep playing |
| POLL-06, QUIZ-08 | Read every host/status string through a full game | Game = whole play-through; Round only for prompts; flag any leftover "Round N" label for the session |
| POLL-04 | Home page, open invite link, set PIN, press Back | Lands on the previous page, not the used invite |
| POLL-05 | Claim only some seats, start a round-game | "Waiting for X to open their invite link" and a "not joined" chip |
| WORD-03, WORD-04, WORD-08 | Sketch Guess: drag a 4 s scribble while sampling a guesser's canvas ink; draw to the 200-stroke cap; right-click drag | Ink grows during the drag; "The canvas is full" and no phantom strokes; right-click draws nothing |
| WORD-06 | Sketch Guess drawer in phone landscape (844x390) and after rotating | Canvas is about 273 px square, tools reachable |
| REFLEX-07 | Wheel with a screen reader if available, otherwise inspect the live region's text over time | Result text is not in the live region until the wheel stops |
| LEAD-02 | 21 joins in a minute | 429 with `Retry-After` and a JSON body; the join screen shows its own message |
| POLL-08, WORD-12, POLL-09 (partial), QUIZ-07 (heading), STORY-04, POLL-10 (rank) | Survey reveal, Word Spies board on a 320/360/390 phone, Never Have I Ever statement, Two Truths headings, Fortunately input, scoreboard at zero | No percentage on survey rows; board words readable and unclipped (owner labels unbroken at 390+); lead-in appears once; no skipped heading level; label/lead-in as in section 1; dash ranks at zero |
| B1 / join flow | Create room, claim every seat through the real claim page, later return via `/room/<slug>/join` with name + PIN | Claim signs in and opens the room; join page names what is missing while its button is disabled |

## 5. Part B: Coverage gaps from the first audit (close all of them)

The first audit left these partial. Cover each properly, per game where it says so.

1. **WebKit, all 19 games**: full lifecycle (create, claim, start, play a full game, reveal, finish, start new game) on desktop WebKit and iPhone WebKit, with real players (4 per game, minimum), including hidden-information checks.
2. **Chrome, Firefox, Brave**: all 19 games at 4 players, one full game each (the first audit did one round). Plus an **8-player** run for every game whose maximum allows it (all 19 allow 12).
3. **Width and zoom sweep**, systematically per game category: 1920, 1440, 1280, 1024, 768, 360, 320; browser zoom 200% and 400%; a **live resize from 1920 down to 320 during play** with no lost state. List breakpoints where anything clips, overlaps, scrolls sideways or becomes unusable.
4. **Keyboard-only** full runs for at least one game in every category, **including Reflex, Story and Word/Draw games** (the first run only did Poll and Two Truths): visible focus, logical order, no traps, Escape closes the confirm, focus returns sensibly. Note Sketch Guess has no keyboard equivalent for drawing (known).
5. **Slow 3G (400 kbps, 400 ms) and offline for every game**: reload on a room, go offline mid-round and back, answer while offline. Record time to skeleton / heading / game UI, and whether state and submissions recover without duplicates.
6. **Lighthouse** (mobile preset, throttled) on the **production build** for: home, game picker/create screen, join page, and 3+ in-room game screens (one per category type). Record LCP, FCP, CLS, TBT, JS transferred. **Measure INP** with a real interaction trace (Lighthouse navigation mode does not produce it). Known baseline: home LCP 1.7 s, join LCP 1.9 s, JS 102 KB gzip, CLS 0.
7. **Resilience:** player refresh in every phase of at least one game per category; close and reopen a tab; host leaves mid-game; late player; duplicate tabs for one player; **token expiry** (a correctly signed token with a past expiry made in-process, never printed). The first audit could not explain the result after a server restart ("only 1 of 4 clients showed results within 3 s"): **re-test it with the stale-screen bug now fixed** (2 of 4 answered, restart the API, the other two answer) and report plainly whether it reproduces.
8. **Accessibility** with axe-core on every meaningful state of every game, plus `npm run check:contrast`, reduced motion, 320 px and 400% reflow, and the confirm control (focus, Escape, screen-reader name).
9. **Security / hidden information** again for the games where it matters (Word Spies key, Forbidden Words card, Sketch word, Bingo cards, Fill-in text, Two Truths lie, Trivia correct answer, poll answers before reveal), on the WebSocket frames **and** the DOM, for host, player and a not-yet-joined seat. Plus host-only actions from a non-host, answering twice, out-of-phase actions, replaying a request. Check that the new `sequence` number leaks nothing useful.
10. **Emulation-only claims:** re-check the "page widens" and landscape findings on iPhone WebKit and Pixel/Android Chromium emulation, and say clearly what still needs a real device.

## 6. Part C: New-code review surface (look for fresh bugs)

The fixes added behaviour that has not been through a full audit. Probe especially:

* The per-room snapshot lock: 12 players acting in one second; players joining mid-burst; an action response arriving after a newer push for the *same* player; reconnect storms. Anything that makes a screen older than the server, or a request hang.
* The confirm control: double-tap on a phone, confirm then immediately start a new game, two hosts' tabs, End game while a player is mid-submit.
* The new `claimed` flag: a seat claimed after the game started; the hint disappearing for everyone; Two Truths voting progress.
* Sketch Guess segmenting: very long continuous scribbles (does the 200-stroke cap now arrive sooner than before?), undo/clear while a stroke is in progress, two-finger touch, pen/mouse differences in Firefox and WebKit, pointer capture on rotation.
* The loading line in `index.html`: any flash, layout shift or duplicate text when React mounts; behaviour with JavaScript disabled.
* Setup messages mirroring server rules: any case where the form says OK but the server answers 400, or the reverse (case sensitivity, accents, emoji, RTL, Luganda/Swahili diacritics).
* Wording consistency after the Round/Game change in every screen, toast, aria-label and error.

## 7. Known and deliberately-open items (do NOT report as new; do re-state their status in a table)

* **G1** invite improvements and **G2** no PIN recovery (design decisions pending).
* **QUIZ-06 remainder**: no caps on trivia question count, options per question, or option length.
* **POLL-09** built-in content is not tuned for the audience (a content decision). The duplicated lead-in *is* fixed.
* **QUIZ-07 remainder**: focus after voting in Two Truths, three live regions.
* **WORD-07** Sketch canvas sits below the fold on a laptop.
* **LEAD-03** the dev-only console message "Failed to start the connection... stopped during negotiation" (React StrictMode). **Not seen in a production build.** If you see it in the production build, that *is* a finding.
* **Sketch Guess has no keyboard equivalent**, Buzzer fairness depends on arrival order, migrations run on startup against the configured database (known gap in `Gaps_Bugs.md`).
* The app has **no spectator role** (so the spec's "spectator view" is BLOCKED: no such feature).
* The game picker has category headings and search but **no category filter** (a proposal, not a bug).

## 8. Execution order and checkpointing

1. Setup and verify environment (section 2), including a WebKit launch check.
2. Build/reuse the harness (first prompt, section 4.2) with the pacing governor; smoke-test with Random Picker, 4 players, in **Chrome and WebKit**.
3. Part A regression (high value, do it first; report any regression immediately in your notes).
4. Part B full lifecycles for all 19 games across browsers, with hidden-information, axe and frame logs captured in the same runs.
5. Part B width/zoom, keyboard, slow network, Lighthouse/INP.
6. Part C probes.
7. Resilience, with the **server restart last**, after everything else, so it cannot disturb other tests.

Write `<scratchpad>/coverage.json` and `<scratchpad>/findings.json` after **every game**. If your context is compacted or interrupted, resume from those files and do not re-test or re-decide. A cell is PASS only if the script run and its captured output exist on disk.

If you can spawn subagents, split by category (Poll / Quiz / Reflex / Story / Word-Draw) with **at most two at a time**, shared read-only harness, separate room codes and scratchpad folders, and the lead re-verifies every Blocker and Major finding itself.

## 9. Deliverable: `QA_REPORT_2.md`

Use the structure in `docs/QA_PROMPT.md` section 26, plus these additions:

1. **Executive summary** that states plainly: games tested, per-game number of simultaneous real players, browsers/viewports covered, counts by severity, **number of regressions**, and anything blocked.
2. **Regression table** (Part A): every row PASS/FAIL with evidence and the earlier ID.
3. **Comparison with the first audit**: findings fixed and confirmed, fixed but only partly confirmed, regressed, and new.
4. **Coverage matrix** (game x Chrome / Firefox / Brave / WebKit / phone portrait / phone landscape / tablet / 8 players) with PASS / FAIL / NOT TESTED / BLOCKED and a technical reason for every non-PASS.
5. Findings (severity Blocker / Major / Minor / Polish; reproduction steps, expected, actual, impact, evidence filename, console/network info, suggested direction). Predictable evidence names: `<game>-<browser>-<viewport>-<issue>.png`. Redact PINs, tokens and invite tokens everywhere.
6. Cross-game patterns and the shared component likely responsible.
7. Accessibility, performance (actual numbers, including INP), and security/information-leak sections, separating *confirmed issue* / *tested and passed* / *could not test*.
8. The known-open table (section 7) with current status.
9. Self-audit: what was covered, what was not, assumptions, anything environmental (network flapping, timing noise) that lowers confidence.

## 10. Final chat response

Only a concise summary: games tested and with how many real players, number of **regressions**, Blockers/Majors, finding counts by severity, the most important cross-game patterns, whether the production build and WebKit were covered, anything blocked, and the path `QA_REPORT_2.md`. Do not claim full coverage if any significant part was blocked.
