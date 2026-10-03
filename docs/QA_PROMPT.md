# QA TASK: End-to-End QA Audit of "The Playground"

You are a **senior QA engineer, browser automation engineer, and UI/UX reviewer**.

Your task is to perform a **real, evidence-backed end-to-end QA audit** of every game in **The Playground** in:

`/home/amos/dev/dotnet/Choice_Maker`

You must test the application through **real browser interactions**, using multiple simultaneous players where required. Do not merely inspect source code and infer behaviour.

Your final output is a factual QA report containing bugs, UX problems, accessibility issues, responsive problems, performance observations, security observations, and missing features.

---

# 1. NON-NEGOTIABLE RULES

1. **DO NOT modify application source code.**
2. **DO NOT fix bugs.**
3. **DO NOT change configuration merely to make a test pass.**
4. You may create temporary scripts, traces, screenshots, logs, and test artifacts, but put them under a dedicated scratchpad directory outside the repository.
5. The only file you may create inside the repository is:
   `/home/amos/dev/dotnet/Choice_Maker/QA_REPORT.md`
   The existing local `.env` must be used as-is and never edited (see Section 28).
6. Do not commit anything.
7. Never claim that something passed unless you actually tested it.
8. If something cannot be tested, mark it **Not Tested / Blocked** and explain exactly why.
9. Do not guess missing configuration, credentials, expected behaviour, or undocumented requirements.
10. Distinguish clearly between:

* **Bug** — something behaves incorrectly.
* **UX issue** — behaviour works but creates unnecessary confusion/friction.
* **Accessibility issue**
* **Responsive/UI issue**
* **Performance issue**
* **Security issue**
* **Content/copy issue**
* **Missing feature / proposal**

11. Read `Gaps_Bugs.md` before testing.
12. Known issues from `Gaps_Bugs.md` must be **re-tested**, but must not be counted as newly discovered bugs. Record their current status separately.
13. **Cost is never a reason to reduce coverage** (see Section 4.1).
14. **Never print, quote, or copy the contents of `.env`** into logs, screenshots, filenames, the report, or your chat messages. Refer to configuration only by variable name.

---

# 2. FIRST: UNDERSTAND THE APPLICATION

Before interacting with the application:

1. Read:

   * `README.md`
   * `Gaps_Bugs.md`
   * relevant frontend documentation
   * `backend/RandomRoom.Api/.env.example`

2. Identify:

   * how the application is started
   * frontend URL/port
   * API URL/port
   * database requirements
   * authentication model (verify against the code; the README's "Auth model" section is outdated, see Section 28)
   * player/session model
   * host model
   * available games
   * expected player counts (read each game's `minPlayers`/`maxPlayers` from `frontend/src/games/registry.ts` and the game modules)
   * any documented limitations
   * the rate limits that apply to the harness (see Section 28)

3. Do not infer undocumented behaviour as a requirement.

If the documentation conflicts with the actual application, record the discrepancy.

---

# 3. START THE APPLICATION

The services are normally **already running**. Check first, and reuse them. Do not restart them or start extra instances.

Expected local services:

* PostgreSQL: Docker container `randomroom-pg`, port `5433`
* API: `:5184`
* Frontend: `:5173` (`npm run dev` in `frontend/`, proxies `/api` and `/hubs` to the API)

Only if a service is not running, start it as documented in `README.md` ("Run locally").

The local `.env` already exists. Use it as-is (Section 28). Do not invent values for missing secrets or credentials.

Confirm that:

* database is healthy
* API is reachable
* frontend is reachable
* API can communicate with the database
* frontend can communicate with the API
* the configured connection string points at `localhost:5433` (check the host only, never print the password). If it points anywhere else, **stop and report it**. Never run the audit against a remote or production database.

If setup fails:

1. Capture the exact error.
2. Record what dependency/configuration is missing.
3. Do not guess or fabricate a workaround.
4. Continue with any testing that is genuinely possible.
5. Clearly mark blocked coverage in the final report.

---

# 4. MULTI-PLAYER TEST ENVIRONMENT

These games are multiplayer and server-authoritative. Every game requires at least 4 simultaneous, independent player sessions (separate browser contexts, each with its own token/storage). Word Spies needs 4+, and 4 is the baseline for all other games.

## 4.1 Cost is not a valid reason to reduce coverage

* Run length, token usage, or the number of browser contexts required is **NEVER** a justification for skipping a game, reducing the player count below the game's documented minimum, simulating other players, or marking something NOT TESTED.
* Never replace real browser players with direct API/SignalR calls for flows that are supposed to be exercised through the UI. Direct protocol clients are allowed only as **ADDITIONAL** probes (race conditions, unauthorized actions, payload inspection), never as a substitute for real UI players.
* "NOT TESTED" and "BLOCKED" are reserved for genuine technical blockers (unsupported browser, crashed service, a game whose maximum player count is below what a test needs) and must name that blocker. "It would take too long" is not a blocker.

## 4.2 Build the harness once, then reuse it for every game

Before testing any game, build a reusable Playwright harness in the scratchpad (not the repo):

1. A fixture that launches one browser and creates N isolated contexts (default 4, configurable up to 8 for scoreboard and long-list tests). Each context is one player in the room, with its own storage.
2. Players are created the way real users create them (Section 28): the host creates a room through the UI and names the players, then every player opens their own invite link in their own context and sets their own PIN through the real claim screen. Names and PINs are generated by the harness and kept in memory only.
3. Helpers: `createRoom(host)`, `claimSeats(players, invites)`, `joinRoom(players, slug)` (name + PIN on the join screen, for re-join and resilience tests), `startGame(host, gameId, settings)`, `waitForPhase(page, phase)`, `submitAll(players, fn)`, `snapshot(players, label)`.
4. A **pacing governor** shared by all helpers, so the harness stays inside the API rate limits (Section 28, point 5). It must space out room creations, invite claims and joins, and handle `429` by waiting for the window to reset and retrying, and must record each `429` it hit.
5. Per-player capture, enabled by default: console log, network log, SignalR/WebSocket frame log, screenshot on failure, and a trace per context. Redact PINs, tokens and invite tokens from everything you keep.
6. An axe-core injector callable on any page at any state.
7. Role views (host / player / spectator) available as named handles, so each game script can assert what each role should and should not see.
8. Smoke-test the harness on one simple game (Random Picker) with 4 players before building on it. If the harness cannot get 4 players into one room, stop and report that as a **Blocker** with the exact error. Do not drop to fewer players.

## 4.3 Scripted, not click-by-click

Each game's lifecycle is a deterministic Playwright script built on the harness, run headless. Do not drive each click of each player through individual agent tool calls. Write the script, run it, read the structured output (assertions, captured logs, screenshots), and only open traces or screenshots where something failed or looks suspicious. Use headed or visual inspection only for things a script cannot judge (layout, animation feel, copy clarity).

## 4.4 Parallelism (use only if the Agent tool is available)

If you can spawn subagents, split by game category, not by environment:

* **Poll:** Would You Rather, This or That, Most Likely To, Never Have I Ever, Survey Showdown
* **Quiz:** Trivia, Name That Song or Movie, Guess Who Wrote It, Two Truths and a Lie
* **Reflex:** Buzzer Round, Spin the Wheel, Bingo, Random Picker
* **Story:** Fill-in Stories, One-Word Story, Fortunately/Unfortunately
* **Word/Draw:** Word Spies, Forbidden Words, Sketch Guess

Rules for parallel agents:

* The harness is built first by the lead agent and shared read-only.
* All agents use the SAME running database/API/frontend. Do not start extra instances.
* Each agent uses its own room codes and its own scratchpad subfolder (`<scratchpad>/<category>/`). No shared mutable files.
* Rooms are never reused across agents, and agents never touch another agent's rooms.
* Player names are scoped to a room, so identities cannot collide as long as every agent creates its own rooms. Prefix generated names with the category to keep logs readable.
* **At most 2 agents run concurrently.** All agents share one IP, so the API rate limits are one shared budget (Section 28, point 5). Every agent must use the harness's pacing governor. Run the remaining categories after the first two finish.
* Server-restart and DB-level resilience tests are run ONLY by the lead agent, after all category agents have finished, because they disrupt everyone.
* Each agent writes its results to `<scratchpad>/<category>/results.json` (coverage matrix cells + findings with evidence paths) as it goes, not only at the end.
* The lead agent merges all results, re-verifies every Blocker and Major finding itself, and writes the single `QA_REPORT.md`.
* If subagents are not available, run the same categories sequentially, in the same order, with the same checkpoint files.

---

# 5. BROWSER / DEVICE MATRIX

## Desktop

Run the full Chrome/Chromium pass.

Browsers:

* Chrome: `/usr/bin/google-chrome`
* Firefox: `/usr/bin/firefox`
* Brave: `/usr/bin/brave-browser`
* Playwright WebKit as a Safari approximation

For Firefox and Brave, at minimum perform this smoke flow for **every game**:

`Create room → Claim seats and join → Play one complete round → Results/Finish`

If Chrome reveals a browser-specific or suspicious issue, reproduce it in Firefox, Brave, and/or WebKit where relevant.

## Desktop widths

Test:

* 1920px
* 1440px
* 1280px
* 1024px
* 768px

Also resize a live session from approximately `1920px` down to `320px` and record meaningful breakpoint failures.

## Mobile viewports

Test:

* iPhone SE: `375x667`
* iPhone 14/15: `390x844`
* Pixel 7: `412x915`
* small Android: `360x640`
* iPad portrait: `768x1024`
* iPad landscape: `1024x768`

Test both:

* portrait
* landscape

Pay particular attention to landscape because fixed headers, viewport height, keyboards, and game controls often break there.

## Zoom / large text

Test:

* browser zoom: 200%
* browser zoom/reflow: 400%
* approximately 320px width

If an OS-level large-text setting can be meaningfully simulated, test it as well.

The UI must not clip, overlap, or become unusable.

---

# 6. GAMES

Test all 19 games:

1. Random Picker
2. Trivia
3. Would You Rather
4. This or That
5. Most Likely To
6. Never Have I Ever
7. Survey Showdown
8. Two Truths and a Lie
9. Guess Who Wrote It
10. Name That Song or Movie
11. Spin the Wheel
12. Buzzer Round
13. Bingo
14. Fill-in Stories
15. One-Word Story
16. Fortunately/Unfortunately
17. Word Spies
18. Forbidden Words
19. Sketch Guess

Do not skip a game merely because another game has similar functionality.

---

# 7. FULL GAME LIFECYCLE

For every game, test:

`Game picker → Setup → Validation → Start → Every phase → Reveal/results → Scoreboard → Finish → Restart/back to picker`

For each stage verify:

### Game picker

* card rendering
* icon/glyph
* category
* description
* player-count requirements
* search
* category filters
* disabled/unavailable states

### Setup

Test:

* default values
* empty input
* invalid input
* excessively long input
* minimum/maximum values
* minimum player requirement
* insufficient players
* built-in content bank
* custom content
* validation messages
* submit/start behaviour

### Multiplayer views

Verify independently:

* host view
* normal player view
* spectator/non-participant view

Each must receive and display only the information appropriate to that role.

### Game state

Verify:

* phase transitions
* timers
* submissions
* reveals
* results
* scoring
* ties
* scoreboard totals
* finish state
* restart behaviour

---

# 8. SECURITY / HIDDEN INFORMATION

This is a **browser-level security review, not a penetration test**.

The server is authoritative, so do not assume information is protected merely because it is hidden visually.

For games involving secret information, inspect:

1. rendered UI
2. DOM
3. browser console
4. network requests
5. SignalR/WebSocket payloads

Verify that information that should be secret is not delivered to unauthorized clients.

Pay particular attention to:

* Word Spies — spy key
* Sketch Guess — secret word for non-drawers
* Forbidden Words — restricted card information
* Bingo — other players' cards where inappropriate
* Fill-in Stories — blanks/story information
* Poll/Survey answers — answers before reveal
* Two Truths and a Lie — hidden lie information

A hidden DOM element or hidden UI component is **not sufficient protection** if the secret has already been sent to the client.

Collect this evidence **during** the lifecycle runs (via the per-player SignalR/WebSocket frame log and per-role DOM snapshots from the harness), so each game is played once for functional and security coverage rather than twice.

---

# 9. GAME-SPECIFIC TESTS

## Sketch Guess

Test:

* mouse drawing
* touch drawing
* undo
* clear
* pointer capture
* accidental page scrolling
* drawing vs scrolling on mobile
* canvas resizing
* rotation
* stroke synchronization
* visible lag between players

## Spin the Wheel

Verify:

* animation
* final selected result
* whether the visible wheel position agrees with the actual result
* repeated spins
* rapid clicks
* reduced-motion behaviour

## Buzzer Round

Test:

* two players buzzing almost simultaneously
* three or more players buzzing simultaneously
* server-side winner determination
* duplicate clicks
* UI consistency between players

## Bingo

Test:

* valid claim
* invalid claim
* duplicate claim
* simultaneous claims
* scoreboard/result consistency

---

# 10. TIMERS AND SYNCHRONIZATION

For every game with a timer, verify:

* countdown accuracy
* what happens at zero
* phase transition at zero
* timer consistency between players
* clock drift
* refresh during countdown
* backgrounding a browser
* locking/sleeping a phone where emulation permits
* reconnecting after backgrounding

The timer must not rely solely on a local client clock if the game requires authoritative synchronization.

---

# 11. MOBILE / TOUCH QA

Test:

* minimum 44x44px touch targets
* controls that only work on hover
* accidental taps
* double taps
* rapid repeated taps
* repeated submissions

Keyboard behaviour:

* keyboard covering inputs
* keyboard covering submit buttons
* layout shifting on focus
* mobile input zoom
* appropriate `inputmode`
* appropriate autocomplete
* appropriate `enterkeyhint`

Viewport behaviour:

* safe-area/notch handling
* home-indicator area
* `100vh` vs dynamic viewport issues
* browser chrome changing viewport height

Navigation:

* browser Back
* browser Forward
* swipe-back where available
* accidental exit from an active game
* refresh during every game phase
* deep-link reload

---

# 12. ACCESSIBILITY — WCAG 2.2 AA

Run accessibility checks throughout the application.

## Keyboard

Perform a keyboard-only run through at least 5 representative games.

Check:

* visible focus
* logical tab order
* no focus traps
* dialogs close with Escape
* focus returns correctly
* all interactive controls are keyboard accessible

## Semantics

Check:

* landmarks
* heading hierarchy
* form labels
* icon-button accessible names
* dialog semantics
* live regions
* phase-change announcements
* result announcements
* timer announcements without announcing every second

Check appropriate alternatives for:

* icons/glyphs
* canvas content
* visual status indicators

## Contrast

Run:

`npm run check:contrast`

Also manually inspect:

* normal text
* disabled controls
* hover
* focus
* selected
* error states

Colour must never be the only indicator of state.

## Motion

Test `prefers-reduced-motion`.

Pay particular attention to:

* wheel animation
* confetti
* transitions
* game-phase animations

## Automated accessibility

Inject **axe-core through Playwright** and run it on every meaningful screen/state of every game.

Record violations with:

* rule
* affected element
* environment
* evidence

Also test reflow at:

* 320px
* 400% zoom

---

# 13. RESILIENCE / REALTIME

Test:

* disconnect during a round
* reconnect
* refresh during a round
* close and reopen a player tab
* player leaves mid-game
* player rejoins (via the join screen, name + PIN)
* host leaves mid-game
* late player joins (a seat that is claimed after the game has started)
* duplicate tabs for the same player (the join endpoint allows signing in the same player from several contexts)
* token expiry during a game (tokens last 12 hours, so simulate this without waiting; if it cannot be simulated without changing configuration or code, mark it BLOCKED with that reason)
* server restart during a game (lead agent only, after all other testing; see 4.4)

After reconnection verify:

* same game
* same player identity
* correct phase
* correct score
* no duplicated players
* no stale UI
* no duplicated submissions

---

# 14. NETWORK CONDITIONS

Test under:

* normal network
* Slow 3G
* offline

Observe:

* time to interactive
* loading states
* layout shift
* failed requests
* retry behaviour
* error messages
* endless spinners
* reconnect loops

Treat low-bandwidth connectivity as a real target condition.

---

# 15. RACE CONDITIONS

Deliberately create simultaneous actions:

* all players submit at once
* host advances while a player submits
* host clicks Next twice
* player submits twice rapidly
* multiple players claim Bingo simultaneously
* multiple players buzz simultaneously

The server should determine the authoritative result.

The UI should remain consistent across clients.

---

# 16. BROWSER-LEVEL SECURITY TESTS

Attempt, where safely possible from browser tools:

* non-host invoking host-only actions
* answering twice
* answering outside the current phase
* acting as another player
* forbidden self-voting
* reading another player's hidden state
* replaying requests
* rapidly spamming an endpoint/button

Expected behaviour:

* server rejects unauthorized actions
* UI displays a sane user-facing error
* no stack traces
* no raw internal JSON
* no internal implementation details

Check that:

* PINs
* tokens
* invite information

are not unnecessarily exposed through:

* URLs
* console logs
* localStorage/sessionStorage
* visible error messages

Do not perform destructive penetration testing.

Note on rate limiting: deliberate spamming will hit the API rate limits (join 20/min per IP, create-room and claim 40/min per IP). Run these probes **after** the lifecycle runs, in their own room, and make sure the pacing governor is not in use for the probe itself, so the limit's behaviour is observed deliberately. Record whether the UI shows a clear message when throttled.

---

# 17. PERFORMANCE

Run Lighthouse using the mobile preset with throttling on:

* home page
* game picker
* 3 representative game screens

Record actual numbers for:

* LCP
* INP
* CLS
* JavaScript transferred
* relevant resource sizes
* lazy-loaded game modules

Use these reference targets:

* LCP < 2.5s
* INP < 200ms
* CLS < 0.1
* first-load JS target < 150KB compressed

Do not describe performance using only words such as "fast" or "slow". Record measurements.

Also observe:

* console errors
* console warnings
* failed requests
* 4xx responses
* 5xx responses
* SignalR reconnect loops
* memory growth
* CPU growth

For long-session testing, use examples such as:

* 20 Trivia rounds
* extended Sketch Guess session

---

# 18. CONTENT AND UX

Check:

* spelling
* grammar
* unclear instructions
* inconsistent terminology
* missing "How to Play" information
* inappropriate jargon
* confusing error messages
* missing empty states
* missing loading states
* missing success states
* missing error states

Use the terminology consistently:

* room
* game
* session
* player
* host

Flag inconsistencies.

Test long/unusual content:

* very long player names (the database limits names; record the limit and what happens beyond it)
* long custom prompts
* emoji
* RTL text
* non-Latin scripts
* Luganda/Swahili diacritics
* 20+ list items
* 8+ scoreboard players: use the 8-context harness configuration with the largest player count each game allows. If a game's maximum is below 8, test its maximum and mark the 8-player case BLOCKED for that game, citing the game's `maxPlayers`.

The built-in content is intended for **reunions and cell groups**, so flag content that appears unsuitable for that audience.

Ask of every screen:

> Could a first-time user with low technical confidence understand what to do next without assistance?

Record concrete examples where the answer is no.

The join flow deserves specific attention: invite link, setting a PIN, being signed in automatically, then later returning with name + PIN. Judge whether each screen tells the player what is needed and why.

---

# 19. VISUAL CONSISTENCY

Across all games check:

* button styles
* typography
* headings
* cards
* scoreboards
* spacing
* loading states
* empty states
* error states
* icons/glyph sizing
* alignment
* responsive behaviour

Look for problems that appear repeatedly.

If several games exhibit the same issue, identify the likely shared component responsible, such as:

* Button
* Card
* Scoreboard
* Field
* ListEditor
* RoundGameScreen

Do not modify the component.

---

# 20. PRODUCTION BUILD

After the development build has been tested:

Run:

`npm run build`

Then test the production build using:

* Vite preview, or
* Docker image if available

At minimum test production on:

* Chrome desktop
* Chrome mobile emulation

Specifically check for dev/prod differences involving:

* minification
* lazy chunks
* asset paths
* caching
* service workers
* SPA fallback
* deep-link refresh

Do not leave a preview server running on a port the audit does not own, and do not overwrite the dev server's files (build output must not break the running dev setup; if it would, report that as a blocker for this section instead).

---

# 21. EVIDENCE REQUIREMENTS

Every actual finding must have evidence.

Capture, where applicable:

* screenshot
* Playwright trace
* short screen recording for timing/interaction bugs
* console error
* network error/request
* relevant DOM information

Each finding must include:

* finding ID
* game
* category
* severity
* browser
* viewport
* exact reproduction steps
* expected behaviour
* actual behaviour
* evidence filename
* relevant console/network information
* suggested direction for resolution

Use predictable filenames:

`<game>-<browser>-<viewport>-<issue>.png`

Example:

`sketch-guess-chrome-390x844-canvas-scroll.png`

Do not fabricate evidence. Redact PINs, tokens and invite tokens in anything you save.

---

# 22. SEVERITY

Use exactly these severity levels:

### Blocker

Prevents the game/application from being meaningfully used or tested.

### Major

Important functionality is broken, unreliable, insecure, or severely impacts users.

### Minor

A real defect with limited impact or a workaround.

### Polish

Small visual, copy, consistency, or refinement issue.

Do not use severity as an arbitrary opinion. Explain the concrete user impact.

---

# 23. KNOWN ISSUES

`Gaps_Bugs.md` contains previously known issues.

For each known issue:

1. Re-test it.
2. Determine whether it is:

   * still present
   * partially resolved
   * resolved
   * unable to verify
3. Do not count it as a newly discovered finding.
4. Include a concise status table in the report.

Notes on specific entries:

* B1 (join flow) is recorded as fixed. Verify the fix through the real flow: opening an invite link and setting a PIN should sign the player in and open the room; the join screen should say what is still needed while its button is disabled. If it is not actually fixed, mark every downstream game test **Blocked**, not passed.
* G2 (no PIN recovery) is a known gap. Do not report it as new and do not try to work around it.

If a known issue blocks further testing, stop pretending that downstream tests were performed.

---

# 24. TEST EXECUTION STRATEGY

Do not spend excessive time repeating identical checks unnecessarily.

Prioritize:

1. Application setup
2. Multi-player harness (Section 4.2) and harness smoke test
3. Known-issue verification
4. Full Chrome multiplayer lifecycle for all 19 games (with role-based hidden-information evidence captured in the same runs)
5. Mobile/responsive testing
6. Accessibility
7. Realtime/resilience (server-restart tests last, lead agent only)
8. Cross-browser smoke testing
9. Performance
10. Production build

Reuse automation infrastructure where possible, but every game's actual lifecycle must still be exercised.

When an issue affects multiple games, verify enough additional games to establish whether it is a cross-game pattern.

Checkpointing:

* Write the coverage matrix and findings to disk incrementally (`<scratchpad>/coverage.json`, `<scratchpad>/findings.json`) after each game.
* If your context is compacted or a run is interrupted, resume from these files instead of re-testing or re-deciding.
* Never mark a cell PASS in these files unless the corresponding script run and its captured output exist on disk.

---

# 25. COVERAGE INTEGRITY

Maintain a live coverage matrix while testing (persisted per Section 24).

Every game/environment combination must end in one of:

* PASS
* FAIL
* NOT TESTED
* BLOCKED

Do not use "PASS" simply because no issue was noticed.

A test is PASS only when the relevant behaviour was actually exercised and behaved as expected.

A cell may be marked NOT TESTED only with a stated **technical** reason. Cost, time, or effort are not valid reasons.

The final report must list, per game, how many simultaneous real players were used. Any game tested with fewer than its minimum is **not covered** (never PASS).

---

# 26. FINAL REPORT

Create:

`/home/amos/dev/dotnet/Choice_Maker/QA_REPORT.md`

The report must contain:

## 1. Executive Summary

Include:

* number of games tested
* number fully tested
* number partially tested
* number blocked
* browsers covered
* viewport coverage
* counts by severity
* top 5 concrete issues

Do not hide blocked coverage.

## 2. Environment

Document:

* OS
* browser versions
* Node/runtime versions where relevant
* application URLs
* database/runtime setup (host and port only, never credentials)
* the maximum number of simultaneous players used per game, and the rate limits observed (including how many `429` responses the harness hit)
* test date

## 3. Known Issue Status

Show every issue from `Gaps_Bugs.md` and its current status.

## 4. Coverage Matrix

Game ×:

* Chrome
* Firefox
* Brave
* WebKit
* phone portrait
* phone landscape
* tablet

Use:

* PASS
* FAIL
* NOT TESTED
* BLOCKED

Include a column for simultaneous real players used per game.

## 5. Findings

Sort by severity:

1. Blocker
2. Major
3. Minor
4. Polish

For each finding include:

* ID
* game
* category
* severity
* environment
* reproduction steps
* expected
* actual
* impact
* evidence
* console/network information
* suggested direction

## 6. Cross-Game Patterns

Identify repeated issues that appear to originate from shared UI or infrastructure.

## 7. Accessibility Findings

Summarize:

* axe violations
* keyboard problems
* screen-reader/semantic issues
* contrast issues
* reduced-motion issues
* zoom/reflow problems

## 8. Performance Findings

Include actual measurements.

## 9. Security / Information-Leak Findings

Clearly distinguish:

* confirmed issue
* attempted test that passed
* test that could not be performed

## 10. Missing Features / Proposals

Keep these separate from bugs.

Do not present a proposed feature as a defect unless existing requirements/documentation indicate that it should exist.

## 11. Not Tested / Blocked

For every blocked area explain:

* what was supposed to be tested
* why it could not be tested
* what dependency prevented it

## 12. Documentation Discrepancies

List places where the README or `.env.example` disagree with the actual behaviour (for example the outdated auth model).

## 13. Self-Audit

State:

* what was covered
* what was not covered
* assumptions made
* limitations
* areas where confidence is lower

Be honest about uncertainty.

---

# 27. FINAL CHAT RESPONSE

After completing the audit, respond with only a concise summary containing:

* total games tested
* major blockers
* number of findings by severity
* most important cross-game patterns
* whether production testing was completed
* report path

Do not claim complete coverage if any significant portion was blocked.

The final report is the authoritative deliverable.

---

# 28. PLAYER IDENTITIES AND `.env`

This app has no fixed player identities. Players are created per room.

1. Read `README.md` and `backend/RandomRoom.Api/.env.example`, then check the code before trusting them. The README and `.env.example` were corrected after the first audit to describe the invite-and-PIN model; confirm they still match the code, and record any new discrepancy. `Room__PlayerPins__*` and `Room__HostPlayer` are not used and must not be treated as requirements.
2. **Actual model:** the host creates a room, types the player names and picks the host. Each player opens their own invite link (`/room/<slug>/claim/<token>`) once and chooses their own PIN, which also signs them in. Later visits use `/room/<slug>/join` (pick name, enter PIN). No identities come from `.env`.
3. The harness must create every room through the UI (the documented API may be used for setup only when the UI is not the thing under test), claim every seat through the real claim screen, and use generated unique names and PINs held only in memory. Names are scoped to a room, so parallel agents cannot collide as long as each uses its own rooms.
4. **`.env`:** it already exists. Use it as-is. Do not edit it. **Never print, quote, or copy its contents** into logs, screenshots, or the report. Only `Room__JwtSigningKey` and `ConnectionStrings__Default` matter to the audit. Confirm that the connection string's host is `localhost` on port `5433` (check the host only) and abort if it is anything else. Do not modify `.env.example`, and do not add `.env` to git.
5. **Rate limits** (per client IP, so every browser context in the audit shares one budget): join `20/min`, create-room and claim-invite `40/min`. One room costs about 5 create/claim calls and 4 joins, so unpaced runs will be throttled within a minute. Pace the harness (at most 3 rooms per minute across all agents; after a `429`, wait for the window to reset, then retry). A `429` caused by harness speed is **not** a finding. Separately, verify that a user who hits the limit sees a clear message (Section 16).
6. **Scoreboard-scale test:** use the largest player count each game allows, up to 8. Read `maxPlayers` from the game registry. BLOCKED only for games whose maximum is below 8.
7. **Host:** the host is whichever name the room creator picks as host when creating the room. Confirm in each room that the host can perform host-only actions and that other players cannot.
8. **Parallel agents** (Section 4.4): at most 2 concurrently, to stay within the rate-limit budget. Server-restart tests remain lead-only.
9. **Known gap G2:** there is no PIN recovery. A player who forgets their PIN cannot get back into that seat. Do not report it as new, and do not work around it.
