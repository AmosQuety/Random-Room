# QA Report: The Playground

Audit date: 2026-10-02 to 2026-10-03. Method: scripted Playwright with real, isolated browser contexts per player; the application source was not modified. All scripts, traces, screenshots and logs are in the scratchpad directory outside the repo, so evidence paths below are relative to `/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/`. PINs, tokens and invite tokens were redacted from everything saved. `.env` was used as-is and never printed.

## 1. Executive Summary

- **Games exercised with real players:** 19 of 19, each with 4 simultaneous players, plus an 8-player run per category (Trivia 4 and 8; every game has maxPlayers 12).
- **Fully tested:** 0. **Partially tested:** 19. No game is complete across the whole matrix because **WebKit could not be launched** on this host (missing system libraries; blocked for every game). Other gaps are listed in section 11.
- **Browsers covered:** Chrome (full), Firefox and Brave (one full round per game), Chrome mobile and tablet emulation. WebKit: blocked.
- **Viewports:** 390x844, 844x390, 768x1024 per game; desktop 1280x800 plus 320px checks on some games. The full 1920/1440/1024/768 width sweep and a live 1920 to 320 resize during play were **not** done systematically.
- **Findings by severity:** Blocker 0, Major 8, Minor 20, Polish 19 (total 47).
- **No Blocker.** The harness got 4 and 8 players into every game.

**Top issues**
1. **Stale screen after simultaneous actions (Major, shared code).** After several players act at once, some clients keep showing the old state while their own WebSocket log shows the newer one. Seen in 5 categories (POLL-01, QUIZ-01, REFLEX-03, STORY-01, WORD-02) and confirmed by the lead in dev and in the production build. Cause: the hub push and a player's own action response both call setSnapshot with no ordering (useRoom.ts:40, RoomScreen.tsx:67). When the host is stale there is no Next or Finish button until a refresh.
2. **Trivia leaks answer correctness while a question is open (Major, QUIZ-02).** Other players' per-answer correct/incorrect flags are sent in the activity feed before the question closes, so a late answerer can work out the right option.
3. **Spin the Wheel shows the result early from spin 2 (Major, REFLEX-01).** The banner appears about 1s after the click, mid-spin, and the host's Award and Next buttons unlock immediately.
4. **Bingo text overflows its cells on phones (Major, REFLEX-02).** Long unbroken items are clipped and the page widens at 320px.
5. **Room creation returns a 500 for names over 32 characters (and other long inputs).** The form allows 40 characters (POLL-02, QUIZ-06, REFLEX-06).

## 2. Environment

- OS: Linux 7.0.0-30-generic. Node v22.22.1, Playwright 1.63.0, axe-core via @axe-core/playwright 4.13, Lighthouse 13.5.
- Browsers: Google Chrome 152.0.7977.75, Firefox 155.0.1, Brave 152.1.94.119 (all system builds driven through Playwright). WebKit blocked.
- URLs: frontend http://localhost:5173 (dev), API http://localhost:5184, production build served by vite preview on :4173 (stopped afterwards).
- Database: PostgreSQL in Docker (randomroom-pg), host localhost, port 5433. The connection string host was checked and is local.
- Players: 4 per game on all browsers, 8 on Chrome per category. Max players allowed by every game is 12.
- Rate limits observed: join 20 per minute per IP (lead probe: 18 failed joins then 429 from the 19th, because 2 joins had already been used). The pacing governor kept the harness inside create/claim limits; one agent reported a single 429 on /api/join from the shared IP budget and retried. Harness 429 log: scratchpad/429.log.
- Environment caveats: the host's network flapped (about every 30s) during part of the run, which dropped Vite's dev socket and SignalR connections for some agents. The reflex agent ran its kept runs in a private network namespace with the Vite HMR socket stubbed; the story agent stubbed the same socket. The lead re-verified the Major findings in the normal environment. The frontend and API were restarted twice (once by an agent, once by the lead for the server-restart test).

## 3. Known Issue Status

| ID | Status | Notes |
| --- | --- | --- |
| B1 join flow | **Resolved** | Claiming an invite signs the player in and opens the room. Verified in every run (4 and 8 players; Chrome, Firefox, Brave). One agent's run needed a fallback to the join screen after the dev socket dropped. |
| B2 search icon size | **Resolved** | Search icon is sized correctly in the picker (screenshot poll-picker-chrome-1280x900-categories.png). The suggested regression test was not checked. |
| G1 better invites | Known gap, not re-reported | |
| G2 no PIN recovery | Known gap, not re-reported | |
| Migrations on startup | Not tested | Not exercised. |
| Stale local server | Not tested | Not exercised. |
| Sketch Guess no keyboard equivalent | **Still present** | Confirmed by the word agent. |
| Buzzer fairness depends on arrival order | **Still present** (as documented) | Winner is whichever request reaches the server first; races always gave exactly one winner. |
| Leftover drawing strokes | Not tested | |

## 4. Coverage Matrix

Cell text is the first word of the agent's result; the reason for each FAIL is in the findings. WebKit is BLOCKED everywhere.

| Game | Players used | chrome | firefox | brave | webkit | phone_portrait | phone_landscape | tablet |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Random Picker | 4 per run, up to 8 on the scoreboard run | FAIL | PASS | PASS | BLOCKED | PASS | PASS | PASS |
| Trivia | up to 8 | FAIL | PASS | FAIL | BLOCKED | FAIL | FAIL | FAIL |
| Would You Rather | 4 per run, up to 8 on the scoreboard run | FAIL | PASS | FAIL | BLOCKED | FAIL | PASS | FAIL |
| This or That | 4 per run, up to 8 on the scoreboard run | FAIL | PASS | FAIL | BLOCKED | FAIL | PASS | FAIL |
| Most Likely To | 4 per run, up to 8 on the scoreboard run | FAIL | PASS | FAIL | BLOCKED | FAIL | PASS | PASS |
| Never Have I Ever | 4 per run, up to 8 on the scoreboard run | FAIL | PASS | FAIL | BLOCKED | FAIL | PASS | PASS |
| Survey Showdown | 4 per run, up to 8 on the scoreboard run | FAIL | PASS | FAIL | BLOCKED | FAIL | PASS | FAIL |
| Two Truths and a Lie | up to 8 | FAIL | PASS | FAIL | BLOCKED | FAIL | FAIL | FAIL |
| Guess Who Wrote It | up to 8 | FAIL | PASS | PASS | BLOCKED | FAIL | FAIL | PASS |
| Name That Song or Movie | up to 8 | FAIL | PASS | PASS | BLOCKED | FAIL | FAIL | PASS |
| Spin the Wheel | 4 per run, up to 8 on the scoreboard run | FAIL | FAIL | FAIL | BLOCKED | PASS | PASS | FAIL |
| Buzzer Round | 4 per run, up to 8 on the scoreboard run | PASS | PASS | PASS | BLOCKED | PASS | PASS | PASS |
| Bingo | 4 per run, up to 8 on the scoreboard run | PASS | PASS | PASS | BLOCKED | FAIL | PASS | PASS |
| Fill-in Stories | up to 8 | FAIL | PASS | PASS | BLOCKED | FAIL | FAIL | PASS |
| One-Word Story | up to 8 | PASS | PASS | PASS | BLOCKED | FAIL | FAIL | PASS |
| Fortunately/Unfortunately | up to 8 | FAIL | FAIL | FAIL | BLOCKED | FAIL | FAIL | FAIL |
| Word Spies | up to 8 | FAIL | PASS | PASS | BLOCKED | FAIL | FAIL | PASS |
| Forbidden Words | up to 8 | PASS | PASS | PASS | BLOCKED | FAIL | FAIL | PASS |
| Sketch Guess | up to 8 | FAIL | FAIL | FAIL | BLOCKED | FAIL | FAIL | FAIL |

Many FAIL cells are caused by the shared stale-screen bug or the shared reveal-animation overflow (QUIZ-04, STORY-02, WORD-05, mobile emulation only), not by a game-specific fault. Per-cell detail: scratchpad/<category>/results.json.

## 5. Findings

### Blocker

None.

### Major

### POLL-01 (Major): all five (shared RoundGameScreen/useRoom)
- **Category:** Bug (realtime)
- **Environment:** Chrome, Brave (also mobile/tablet emulation); not seen in Firefox smoke (1 round each), 1280x800, 390x844, 768x1024
- **Verified by lead:** YES. Own 8-player Would You Rather runs in plain Chrome (verify_stale/v2.mjs): 2 to 4 of 8 clients stayed on 'Locked in' four seconds after everyone answered, in 3 of 4 runs; host once had no Next button. Code cause read at useRoom.ts:40 and RoomScreen.tsx:67 (both call setSnapshot with no ordering).
- **Steps:** 4+ players in a round game; all players press an answer at about the same time (or host presses Reveal now while others answer). Compare each page with the room state.
- **Expected:** Every client shows the revealed round (host sees Next round).
- **Actual:** One or more clients keep showing 'Locked in' and 'N of M answered' with no results. Cause visible in timeline: ws pushes with phase=revealed arrive first, then that client's own POST /api/room/action response (snapshot taken before reveal, phase=collecting) is applied afterwards and overwrites newer state. Reproduced in 4 of 5 rounds with 4 players (Would You Rather) and in ~every round with 8 players. If the host is the stale one there is no Next/Finish button, so the game cannot be advanced without refresh.
- **Impact:** Players miss the result; host can be unable to continue; looks like a frozen game. Refresh fixes it.
- **Evidence:** poll/stale-would-you-rather-chrome-4p.json (timeline),poll/evidence/would-you-rather-chrome-1280x800-STALE-after-simultaneous-answers-r3-client2.png,poll/evidence/survey-showdown-chrome-1280x800-ERROR8-0.png (host at 7 of 8) vs ERROR8-3.png (revealed),poll/evidence/never-have-i-ever-chrome-1280x800-STALE-r2-client2.png,poll/evidence/most-likely-to-brave-1280x800-STALE-r1-player.png,poll/evidence/most-likely-to-chrome-390x844-STALE-r1-player.png,poll/logs/*-8p.log
- **Console/network:** No errors; HTTP 200 on every action; ws frames for the revealed state present in the stale client's own frame log.
- **Suggested direction:** In RoomScreen.run/applySnapshot, ignore a response snapshot that is older than the current one (add a monotonically increasing version/sequence to RoomSnapshot and compare), or drop the response snapshot and rely on the push/refetch.

### QUIZ-01 (Major): trivia, two-truths, guess-who, name-that (shared RoomScreen)
- **Category:** Bug (realtime/state)
- **Environment:** Chrome, Brave (also Firefox 1 event), 1280x800, 390x844, 844x390, 768x1024
- **Verified by lead:** Root cause YES (same code path as POLL-01, reproduced in Would You Rather). Not re-run in Trivia or Two Truths.
- **Steps:** 1) 4-8 real players in a room. 2) All players act at the same moment (Trivia: answer; Two Truths: vote; Guess Who: guess) so the last action completes the round. 3) Compare each player's screen after 2-5 s.
- **Expected:** Every client shows the new state (next question / reveal) because the server pushed it.
- **Actual:** Some clients (often the host or the last to act) stay on the old state. Trivia 4p: P4 shows 'Question 1 ... Answer locked in' while the other three are on Question 2, until a timer tick 10 s later. Two Truths 8p: after 7 simultaneous votes, 4 of 8 clients still showed '4/5/6 of 7 voted' and the host still had 'Reveal now' (clicking gives 409). Trivia 8p: nearly every question left clients stale; on the final question players stayed on 'Question 20' with timer 0:00. The WS frame log of a stale client shows the newer 'revealed' push was received, yet the DOM shows an older snapshot: the HTTP response to the player's own action is applied after the push (RoomScreen.run -> applySnapshot overwrites the newer state). 3 of 4 simultaneous-answer runs and 1-3 events per 4p game reproduced it.
- **Impact:** Players can be stuck on a finished round, host sees wrong buttons and gets errors, the last Trivia question never shows 'Trivia complete' until reload. Likely to happen in every real group because players answer at the same time.
- **Evidence:** quiz/evidence/two-truths-chrome-1280x800-stale-race-8p-events.json,quiz/evidence/trivia-chrome-1280x800-stale-race-4p-events.json,quiz/evidence/two-truths-chrome-1280x800-stale-host-after-vote.png,quiz/evidence/trivia-chrome-1280x800-stale-final-question.png,runs/quiz-tt-8p/final-quizttP1.log.json (ws frames end in revealed, DOM shows voting)
- **Console/network:** 409 POST /api/room/action on the stale host/player; banner 'The session is not active.'
- **Suggested direction:** Do not apply an action response if a newer push has arrived (add a monotonically increasing version/sequence to the snapshot and ignore older ones), or have actions return nothing and rely on the push plus a refetch.

### QUIZ-02 (Major): trivia
- **Category:** Security / hidden-information leak
- **Environment:** Chrome, 1280x800
- **Verified by lead:** YES. In the saved WebSocket frames (runs/quiz-trivia-8p) the open-question payload, which has no correctIndex, carries other players' per-answer correct flags (83 frames).
- **Steps:** 1) Start Trivia with 4 players. 2) One player answers the open question. 3) Look at the Activity list on any other player's screen before they answer.
- **Expected:** Whether an answer is right stays hidden until the question closes (the game promises 'nobody can cheat').
- **Actual:** The Activity list appears immediately: '<player> answered incorrectly' (red X) or 'correctly' (green tick) for the question that is still open. Payload field activity[].correct is sent in the live snapshot. Others can read the correct option by elimination (2-option questions are decided by one answer).
- **Impact:** Late answerers can copy the result; competitive scoring is compromised.
- **Evidence:** quiz/evidence/trivia-chrome-1280x800-activity-leak.png,runs/quiz-trivia-chrome/final-quiztrP1.log.json (activity[].correct in frames)
- **Console/network:** none
- **Suggested direction:** Only include activity entries for closed questions, or omit the 'correct' flag until the question is closed.

### REFLEX-01 (Major): spin-wheel
- **Category:** Bug
- **Environment:** chrome, firefox, brave, 1280x800, 768x1024
- **Verified by lead:** YES. Re-ran reflex/wheel.mjs on Chrome: spin 1 correct, spin 2 shows the result at about 1.1s while the disc is still turning, on all 4 clients.
- **Steps:** Start Spin the Wheel (4p). Spinner spins in spin 1: result text appears after ~4s. Host presses Next spin; spinner of spin 2 spins; look at result text and host Award/Next buttons at 0.7s.
- **Expected:** Every spin: wheel animates ~4s, result text and Award/Next stay hidden/disabled until it stops.
- **Actual:** From spin 2 onward the result banner is visible about 0.9s after the click while the disc is still turning, and Award a point / Next spin are enabled immediately. Spin 1 behaves correctly (4.8s). Reproduced on 4, 8 players and in Chrome, Firefox, Brave, tablet. Not visible with reduced motion. Cause (code): useLanded state in Turn() is never reset between rounds because Turn is not remounted (only Wheel has key=round).
- **Impact:** Spoils the result before the wheel stops on every spin after the first; undermines the animation and lets host skip ahead.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/reflex/evidence/spin-wheel-chrome-1280x800-round2-result-spoiled-midspin.png; checks-wheel-*.json
- **Console/network:** no console errors
- **Suggested direction:** Reset landed per round (key Turn by payload.round or reset the hook when last becomes null).

### REFLEX-02 (Major): bingo
- **Category:** Responsive/UI issue
- **Environment:** chrome, 390x844, 320x640
- **Verified by lead:** Code consistent (grid-cols-5 tracks, cells use break-words but no min-w-0, bingo/GameScreen.tsx:30,42). Not re-run.
- **Steps:** Bingo with items such as BingoItem01x (12 chars, no spaces). Open a player card at 390 or 320 wide.
- **Expected:** Cell text wraps or shrinks to remain readable inside its cell; page does not widen.
- **Actual:** Long unbroken words are clipped and overflow the cells; last column spills outside the card; at 320 the layout viewport grows to 323-399px (horizontal overflow, WCAG 1.4.10 reflow failure). Players cannot read their items.
- **Impact:** A player cannot read items such as long single words and may mark wrongly or miss a call; custom items allow up to 40 chars.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/reflex/evidence/bingo-chrome-390x844-dealt-player.png; bingo-chrome-320x640-gameover-winner.png
- **Console/network:** none
- **Suggested direction:** Allow smaller/auto-fitting text, hyphenation or overflow-wrap:anywhere with min-w-0 on the cells; cap card width.

### REFLEX-03 (Major): random-picker
- **Category:** Bug (stale UI)
- **Environment:** chrome, 1280x800
- **Verified by lead:** Pattern YES: own Random Picker runs showed a client with 1 of 4 result cards and no Final result after Completed arrived over its WebSocket, on both the production build (1 of 2 runs) and dev (1 of 1).
- **Steps:** 8 players. All 8 click Let randomness decide at once (one double-clicks).
- **Expected:** All clients show Final result.
- **Actual:** Server and every client ws received session Completed with 8 triggers, but 3 of 8 clients still showed no Final result after 15s (own action response overwriting newer pushed snapshot; same pattern as POLL-01/QUIZ-01). 4-player run did not reproduce.
- **Impact:** Some players never see the final result until refresh.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/reflex/evidence/random-picker-chrome-1280x800-final-player.png; checks-picker-chrome-eight.json
- **Console/network:** ws log runs/rpchei: Completed snapshot received by all
- **Suggested direction:** Ignore action-response snapshots older than the latest pushed version (use a version/sequence number).

### STORY-01 (Major): mad-libs
- **Category:** Bug (realtime/state), cross-game pattern POLL-01/QUIZ-01
- **Environment:** Chrome (2 of 3 Chrome runs: 4p and 8p; not seen in Firefox, Brave, phone, tablet, reduced-motion runs), 1280x800
- **Verified by lead:** Root cause YES (same code path as POLL-01). Not re-run in Fill-in Stories.
- **Steps:** 1) Fill-in Stories room with 4 (or 8) real players. 2) In round 1 all players type their word(s) and press 'Lock it in' at the same moment. 3) Look at the host screen 1-2 s after the last submit.
- **Expected:** Every client shows the revealed story and the host sees 'Next round'.
- **Actual:** The host stayed on 'Round 1 of 3 ... Locked in ... 3 of 4 answered' with a live 'Reveal now' button and a 0 scoreboard while the other players already showed the finished story. The host's WebSocket log ends in a 'revealed' snapshot, so the push was received and then overwritten by the older HTTP response to the host's own action. In the 4p and 8p runs the host had no 'Next round' button until a reload (harness recorded hostStale r1-next).
- **Impact:** Host cannot advance without reloading; scoreboard looks empty for the host.
- **Evidence:** story/evidence/mad-libs-chrome-1280x800-stale-host-after-reveal.png,story/ml-chrome-4p-saved.json,story/story-ml-8p-out.json (hostStale),runs/story-ml-chrome/final-storymP1.log.json (ws frames)
- **Console/network:** No errors; push received, DOM older. Same mechanism as QUIZ-01 (RoomScreen.run applying the action response after a newer push).
- **Suggested direction:** Version snapshots and ignore older ones, as proposed in QUIZ-01.

### WORD-02 (Major): word-spies
- **Category:** Bug (realtime/stale UI)
- **Environment:** chrome, 1280x800 (8p, also seen 1 of 3 earlier 6p/8p trials)
- **Verified by lead:** Root cause YES (same code path as POLL-01). Not re-run in Word Spies.
- **Steps:** 8 players. Spymaster gives a 7/9-card clue. Three operatives of that team click different own cards at the same instant (DOM click in parallel). Wait 6 s, compare DOM turned-card count and 'guesses left' with the last roomChanged frame each client received.
- **Expected:** All 8 clients show the same board (3 turned) and same guesses left.
- **Actual:** Server turned 3 and every client's last ws frame says 3, but 1-2 clients (the clickers) kept showing 2 (or 1) turned cards and 'guesses left' one higher for 6+ s, until a later event or reload. Same cause as POLL-01/QUIZ-01: a client's own action response overwrites a newer pushed snapshot. Not reproduced on every trial (6p 2-clicker trials: 0/3; 8p 3-clicker: 2/2 runs).
- **Impact:** Operatives see an out-of-date board; clicking a card that is already turned gives a 409, or they think a guess did not count. Host/teammates could be misled about turn state.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/word-spies-chrome-1280x800-stale-ops0-trial0.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/word-spies-chrome-1280x800-stale-ops1-trial0.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/ws-stale-timeline8.json,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/checks-ws8.json
- **Console/network:** no console error; 409 on a follow-up click
- **Suggested direction:** Same fix as POLL-01: reconcile action responses by snapshot version/sequence (ignore responses older than the last pushed snapshot) in the shared room hook.

### Minor

### POLL-02 (Minor): all (room creation)
- **Category:** Bug
- **Environment:** Chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Create a room with a player name of 33 to 40 characters (the Players field allows 40). Same through POST /api/rooms; title of 81 chars also 500 via API.
- **Expected:** Either the field stops at the real limit (32) or a clear validation message.
- **Actual:** 500 and the generic message 'Something went wrong.' at the last step; 32 chars works.
- **Impact:** Host cannot create the room and is not told why.
- **Evidence:** poll/evidence/would-you-rather-chrome-1280x800-player-name-33-40-chars-500.png,poll/probes-setup.json
- **Console/network:** 500 POST /api/rooms. DB column limit 32 (RoomDbContext) vs UI maxLength 40 and no server check.
- **Suggested direction:** Validate name/title length server-side (400 with message) and align the input maxLength to 32.

### POLL-03 (Minor): all five
- **Category:** UX issue
- **Environment:** Chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Lose the race for a late answer ('Answers are closed for this round.') or answer while offline, then continue to the next round / go back online.
- **Expected:** Error clears when the situation changes.
- **Actual:** The red alert from the earlier failure stays on screen in the next round and after reconnecting ('Could not reach the server...').
- **Impact:** Players think the current round is also closed or the connection is still broken.
- **Evidence:** poll/evidence/would-you-rather-chrome-1280x800-online-after-short-offline.png,poll/checks-poll-would-you-rather-chrome-desktop.json
- **Console/network:** 409 on POST /api/room/action
- **Suggested direction:** Clear the error in RoomScreen when a new snapshot (round/phase change) arrives or on reconnect.

### POLL-04 (Minor): all five
- **Category:** UX issue / Security (minor)
- **Environment:** Chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Claim invite and play; press browser Back during an active round.
- **Expected:** Stay in the room or leave to a safe page.
- **Actual:** Lands on /room/<slug>/claim/<token>, an empty PIN form for the already-used invite; typing a PIN gives 'This invite link is invalid or has already been used.' Forward returns to the room. The invite token remains in browser history.
- **Impact:** Accidental exit from a live game looks like a broken invite; token lingers in history (one-time, so low risk).
- **Evidence:** poll/evidence/would-you-rather-chrome-1280x800-back-during-game.png,poll/checks-nav.json
- **Console/network:** none
- **Suggested direction:** Use history replace when leaving the claim screen so Back skips it.

### POLL-05 (Minor): all five
- **Category:** UX issue
- **Environment:** Chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Start a round while one seat is unclaimed; all claimed players answer.
- **Expected:** Host is told who has not joined, or the round can complete.
- **Actual:** Round waits indefinitely ('7 of 8 answered'); the missing player is shown like any slow answerer. No hint that the seat is unclaimed. Host must know to press Reveal now.
- **Impact:** Host thinks the game has hung.
- **Evidence:** poll/evidence/would-you-rather-chrome-1280x800-8p-r1-7of8-answered-player.png
- **Console/network:** none
- **Suggested direction:** Show 'not joined yet' on the chip (snapshot already has claimed/online state), and offer Reveal now prominently.

### QUIZ-03 (Minor): trivia
- **Category:** Bug (UX)
- **Environment:** Chrome, Brave, 1280x800, 390x844
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Play Trivia with a time limit through the last question with all players answering together.
- **Expected:** No error shown at the end of the game.
- **Actual:** Players see a red alert 'The session is not active.' under the header (client ticks / stale answers sent to the completed session return 409). Seen on host in the 20-question run and on 4-5 players in the 8p run.
- **Impact:** Confusing error at the very end of a game.
- **Evidence:** quiz/evidence/trivia-chrome-1280x800-stale-final-question.png
- **Console/network:** 3-5x 409 POST /api/room/action per run
- **Suggested direction:** Stop sending tick once the session is completed; treat 409 'not active' on tick/answer as silent.

### QUIZ-04 (Minor): all four (shared RoundGameScreen / animate-stamp)
- **Category:** Responsive/UI
- **Environment:** Chrome mobile emulation (isMobile, touch), 390x844 and 844x390
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** 1) Play any quiz game on an emulated phone until a reveal or Game over. 2) Inspect window.innerWidth / take a screenshot.
- **Expected:** Layout stays at the device width.
- **Actual:** After the reveal/Game over 'stamp' animation (keyframes stamp starts at scale(1.6)) the emulated page widens to 457-492px (portrait) and 1047-1078px (landscape); header content (avatar/name) is cut off at the old width and the visual viewport scale changes (1.46). Playwright could not click 'Start new round' on portrait Guess Who/Name That because of the scale mismatch. Reproduced in 3 games x 2 orientations; not seen in the 320px desktop run or in the reduced-motion desktop run. Real-device behaviour NOT verified (emulation only).
- **Impact:** Possible zoomed-out, clipped page after every reveal on phones.
- **Evidence:** quiz/evidence/name-that-chrome-844x390-viewport-zoomout.png,quiz/evidence/two-truths-chrome-390x844-viewport-zoomout.png,runs/quiz-*-mob2-*/layout.jsonl (vw column)
- **Console/network:** none
- **Suggested direction:** Avoid animating transform scale beyond the container (wrap in overflow-hidden/clip, or use a smaller scale / opacity only).

### QUIZ-05 (Minor): picker (all quiz games)
- **Category:** Content/copy
- **Environment:** Chrome, 1280x900
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Create room, game picker, type 'quiz' (the placeholder suggests it) into Search games.
- **Expected:** Quiz games shown.
- **Actual:** 0 games shown (search only matches name/hook/description, not category; the 'Quiz' category has 4 games). 'song' correctly finds Name That.
- **Impact:** The suggested search term returns nothing.
- **Evidence:** quiz/evidence/picker-chrome-1280x900-search-and-setup-probes.json
- **Console/network:** none
- **Suggested direction:** Match category label in search or change the placeholder.

### QUIZ-06 (Minor): trivia (and create-room API)
- **Category:** Bug (validation)
- **Environment:** API probe, n/a
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** POST /api/rooms with trivia question text of 5000 chars; or a 100-char player name; or 40 options / 300 questions / 3000-char option. In the UI the Trivia question text and options have no maxLength.
- **Expected:** 400 with a clear message and matching client limits.
- **Actual:** 5000-char question and 100-char name return 500 'Something went wrong.'; 40 options, 300 questions and a 3000-char option are accepted. UI lets the host type 3000+ chars in a question (name field is capped at 40, room name 80, category 40).
- **Impact:** Host sees a generic error after filling a long form; unbounded content can break the play layout.
- **Evidence:** quiz/evidence/trivia-api-limit-probes.json
- **Console/network:** 500 POST /api/rooms
- **Suggested direction:** Add client maxLength and server 400 limits for question/option length and counts.

### REFLEX-05 (Minor): random-picker
- **Category:** UX issue
- **Environment:** chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Setup: two identical choices (Same, Same); Next; add players; Create.
- **Expected:** Duplicate choices flagged in setup (like Bingo/Wheel do).
- **Actual:** Setup accepts it; error "Random Picker needs at least 2 choices." appears only at the last step after Create, far from the field.
- **Impact:** Confusing late error.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/reflex/probe-picker.mjs (console output)
- **Console/network:** POST /api/rooms 400
- **Suggested direction:** Validate de-duplicated count client-side like the other reflex games.

### REFLEX-06 (Minor): random-picker
- **Category:** Security / input validation
- **Environment:** chrome, n/a
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Direct POST /api/rooms probes (additional): 300 choices; one 5000-char choice.
- **Expected:** Bounded and rejected with 4xx like the UI limits (50 choices, 200 chars).
- **Actual:** 300 choices accepted (200); 5000-char choice returns 500 "Something went wrong." (same family as POLL-02/QUIZ-06).
- **Impact:** Server limits not enforced; 500 for oversized input.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/reflex/probe-picker.mjs
- **Console/network:** 500 on POST /api/rooms
- **Suggested direction:** Enforce count and length limits server-side, return 400.

### STORY-02 (Minor): all three story games (shared GameOver / animate-stamp)
- **Category:** Responsive/UI issue
- **Environment:** Chrome mobile emulation (isMobile, touch); not reproduced with prefers-reduced-motion, 390x844, 844x390
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** 1) Play any story game to the end on a mobile viewport (host presses End the story is enough). 2) Watch the 'Game over' banner appear (also the Fill-in Stories reveal).
- **Expected:** Page stays at device width.
- **Actual:** The banner's stamp animation overflows horizontally for a moment (banner right edge 430 px on a 390 px screen). In mobile emulation the layout viewport then stays enlarged after the animation ends (innerWidth 489, innerHeight 1059; landscape 1078 wide; Fill-in reveal 458) so the page remains zoomed out until reload. Playwright taps on 'Start new round' were then intercepted by neighbouring elements (30 s timeout in all 3 portrait runs, which is why the restart step failed there). With reduced motion the width stays 390.
- **Impact:** On a real phone the game-over and reveal screens may render zoomed-out with blank margin; effect on real devices not verified (emulation only).
- **Evidence:** story/evidence/all-story-games-chrome-390x844-gameover-viewport-zoomout.png,story/evidence/one-word-story-chrome-390x844-gameover.png,story/ovf.mjs output (innerW 390 -> 489 at t+0.1 s, stays 489 at t+3 s)
- **Console/network:** none
- **Suggested direction:** Make the stamp animation not exceed the container (smaller scale, overflow-x: clip on the card/body, or animate opacity/translate), in rounds/parts.tsx GameOver and RoundGameScreen's motion-safe:animate-stamp wrapper.

### STORY-03 (Minor): fortunately
- **Category:** Bug (layout / long input)
- **Environment:** Chrome, Firefox, Brave (all), 1280x800, 768x1024, 844x390
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** 1) Fortunately/Unfortunately, 4 players. 2) On a turn type 140 characters with no spaces (e.g. a long URL or 'xxxx...'). 3) Finish the story.
- **Expected:** Long text wraps inside the story card.
- **Actual:** The finished story does not wrap: page scrollWidth 1947 on a 1280 px window (1819 on 768 px) so the page scrolls sideways. On touch landscape (844x390) the page zoomed out to 1820 px and later taps on 'Add it' stopped advancing the story in my script (6 of 8 turns reached; re-run with spaced text completed), so a single long entry can make the game hard to use on a phone.
- **Impact:** Any player can break the layout for everyone; plausible with pasted links.
- **Evidence:** story/evidence/fortunately-chrome-1280x800-long-unbroken-overflow.png,story/evidence/fortunately-chrome-768x1024-long-unbroken-overflow.png,story/evidence/fortunately-chrome-844x390-long-unbroken-zoomout.png
- **Console/network:** none
- **Suggested direction:** Add break-words / overflow-wrap:anywhere on the story paragraph (story-chain/GameScreen.tsx). One-Word (24) and Fill-in (30 chars per word) are short enough not to trigger it in my tests, but the same wrapper rule would protect them.

### WORD-01 (Minor): word-spies
- **Category:** Bug (copy)
- **Environment:** chrome, 1280x800 (8p)
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** 8 players, spymaster of the team in play gives clue count 9, operatives turn all of that team's cards. Game ends 'all-found'.
- **Expected:** Result text names the team that found every word (the winner).
- **Actual:** Headline 'Blue team wins' but sub-line 'The Red team found every word.' (names the LOSING team). Cause: GameScreen END_TEXT line always prints the opposite of the winner; correct only for the assassin ending.
- **Impact:** The end-of-game explanation contradicts the headline on every 'all-found' finish; players may think the wrong team won.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/word-spies-chrome-1280x800-8p-complete-host.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/checks-ws8.json
- **Console/network:** none
- **Suggested direction:** Pick the subject by endReason: assassin -> loser, all-found -> winner (frontend/src/games/word-spies/GameScreen.tsx, over-state line).

### WORD-03 (Minor): sketch-guess
- **Category:** UX / Performance
- **Environment:** chrome, firefox, brave, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Drawer holds the mouse down and scribbles continuously for ~4 s (40 points); a guesser's canvas ink is sampled every 60 ms.
- **Expected:** Guessers see the line appear as it is drawn (or at least within a second or two).
- **Actual:** Guessers saw nothing for the whole 4.2-4.6 s drag (ink delta 0), then the whole stroke appeared ~0.1 s after pointer-up. A short 3-point stroke reaches guessers in 57-133 ms. Cause: the in-progress stroke is only batched on pointer-up (or at 200 points).
- **Impact:** 'Draw it' feels laggy for guessers: long scribbles and fills appear in jumps; guessing is slower for the drawer's audience.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/checks-sk-chrome.json,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/checks-sk-firefox.json,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/checks-sk-brave.json,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/sketch-guess-chrome-1280x800-after-strokes-guesser.png
- **Console/network:** none
- **Suggested direction:** Flush partial strokes every ~160 ms while the pointer is down (server already accepts batches).

### WORD-04 (Minor): sketch-guess
- **Category:** Bug (UX)
- **Environment:** chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Drawer taps 210 quick dots (server cap 200 strokes).
- **Expected:** Rejected strokes disappear from the drawer's canvas, or drawing is blocked, so drawer and guessers see the same picture.
- **Actual:** Server kept 200; the red text 'The canvas is full. Clear it or undo something first.' appears, but the drawer's canvas still shows the rejected strokes (ink 26382 vs 25125 px on a guesser). The drawer sees marks nobody else can.
- **Impact:** Drawer believes guessers can see extra detail; confusing when the round is reached by long drawings.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/sketch-guess-chrome-1280x800-cap-drawer.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/checks-sk-chrome.json
- **Console/network:** 409 POST /api/room/action (x6) plain title, no stack trace
- **Suggested direction:** Drop unacknowledged strokes from the local canvas when the batch is rejected, or stop accepting local strokes once the cap is hit.

### WORD-05 (Minor): word-spies, forbidden-words, sketch-guess
- **Category:** Responsive/UI
- **Environment:** chrome (mobile emulation, touch), 390x844 and 844x390
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** On a phone-emulated context, reach a reveal/round-complete state (Forbidden: Reveal now; Word Spies: assassin; Sketch: correct guess) and read document.scrollWidth.
- **Expected:** scrollWidth stays 390 (no horizontal overflow).
- **Actual:** scrollWidth grows to 454 (reveal) and 489 (game over) and stays; full-page screenshots are 453-492 px wide at 390 and 1036-1078 at 844 (landscape). Gone with prefers-reduced-motion, so it is the 'stamp' animation (same as QUIZ-04). Real-device behaviour not verified.
- **Impact:** Possible zoomed-out/sideways-pannable page right after each reveal on phones.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/forbidden-words-chrome-390x844-overflow-revealed.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/forbidden-words-chrome-390x844-overflow-gameover.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/word-spies-chrome-390x844-complete-operative-wsm390P3.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/sketch-guess-chrome-390x844-revealed-guesser.png
- **Console/network:** none
- **Suggested direction:** See QUIZ-04: animate-stamp starts at scale(1.6); clip the card (overflow-x: clip on the parent) or start the keyframe at scale(1).

### WORD-06 (Minor): sketch-guess
- **Category:** Responsive/UI
- **Environment:** chrome (touch), 844x390 landscape and rotation 390x844 -> 844x390
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Phone landscape as drawer; rotate a live round from portrait to landscape.
- **Expected:** Whole canvas and tools usable.
- **Actual:** Canvas is a 576 px square in a 390 px-high viewport (about 2/3 visible at best), tools at y~859. The canvas has touch-action:none, so only the side margins (~134 px) can be used to scroll. Drawing and ink survive rotation (checked).
- **Impact:** Drawing in landscape means constant scrolling via the margins; the timer scrolls out of view.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/sketch-guess-chrome-844x390-drawer-initial.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/sketch-guess-chrome-390x844-drawer-rotated.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/checks-sk-m844.json,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/checks-sk-m390.json
- **Console/network:** none
- **Suggested direction:** Cap canvas size by viewport height (max-h with aspect-ratio) or side-by-side layout in landscape.

### WORD-09 (Minor): forbidden-words (shared HostControls; also word-spies, sketch-guess)
- **Category:** UX
- **Environment:** chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Host presses the dashed 'Host controls > End round' button during round 1 of 8.
- **Expected:** A button called 'End round' ends the current round, or asks first.
- **Actual:** It ends the whole game for everybody at once (Game over, 'Nobody scored', remaining 7 rounds discarded), with no confirmation. The in-game 'Reveal now' is the one that ends a single round. Word Spies shows both 'End the game' and 'End round' for the same effect. Same pattern as POLL-10/POLL-06.
- **Impact:** One mis-click in front of a group ends the game.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/forbidden-words-chrome-1280x800-end-round-host.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/forbidden-words-chrome-1280x800-end-round-guesser.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/checks-ender.json
- **Console/network:** none
- **Suggested direction:** Rename to 'End game' and add a confirm step; drop the duplicate in Word Spies.

### LEAD-01 (Minor): All (shared)
- **Category:** Performance issue
- **Environment:** Chrome (Lighthouse mobile, production build), mobile emulation
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Build with the scratchpad outDir, serve with vite preview, run Lighthouse mobile on /room/<slug>/join.
- **Expected:** LCP < 2.5s
- **Actual:** LCP 2.8s on the join screen (home page 2.4s, just under target). CLS 0, TBT 10ms, JS 101 KB transferred.
- **Impact:** The page every invited player lands on first is over the LCP target on a throttled phone.
- **Evidence:** scratchpad/lh/home.json, scratchpad/lh/room-join.json
- **Console/network:** none
- **Suggested direction:** Check what the LCP element is on the join screen and whether the preview data fetch blocks it.

### LEAD-04 (Minor): Build config
- **Category:** Documentation / dev workflow
- **Environment:** n/a, n/a
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Read frontend/vite.config.ts.
- **Expected:** `npm run build` is safe to run next to a running dev setup.
- **Actual:** build.outDir is ../backend/RandomRoom.Api/wwwroot with emptyOutDir: true, so `npm run build` overwrites the API's wwwroot. I built with --outDir pointed at the scratchpad instead.
- **Impact:** A developer or tester who runs the documented build command wipes the folder the API serves.
- **Evidence:** frontend/vite.config.ts line 10
- **Console/network:** none
- **Suggested direction:** Document it, or build to a separate dist and copy in the Dockerfile.

### Polish

### POLL-06 (Polish): all five
- **Category:** Content/copy issue
- **Environment:** all, all
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Finish a game.
- **Expected:** One consistent meaning of 'round'.
- **Actual:** Header badge and status say 'ROUND 1 . COMPLETE' / 'Round 1: Round complete' (the session) while the card says 'Round 3 of 3' (the prompt) and the game over banner says 'Game over'. After 'Start new round' the badge says 'Round 2 . LIVE' while the card says 'Round 1 of 3'.
- **Impact:** Confusing for first-time users.
- **Evidence:** poll/evidence/would-you-rather-chrome-1280x800-gameover-player.png
- **Console/network:** none
- **Suggested direction:** Call the session 'Game 1' (or hide it) and keep 'Round' for prompts.

### POLL-07 (Polish): Would You Rather, This or That, Most Likely To, Never Have I Ever
- **Category:** UX issue
- **Environment:** Chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Setup: type only Option A of a custom dilemma.
- **Expected:** Inline message like Survey Showdown's.
- **Actual:** Only aria-invalid and the bottom 'Finish the setup to continue.'; no text beside the row.
- **Impact:** Low-confidence users may not see what is missing.
- **Evidence:** poll/evidence/would-you-rather-chrome-1280x900-setup-partial-row.png
- **Console/network:** none
- **Suggested direction:** Add a visible hint in the shared TwoWayEditor/StatementEditor.

### POLL-08 (Polish): Survey Showdown
- **Category:** Content/copy issue
- **Environment:** Chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Reveal a round.
- **Expected:** Meaningful percentage or none.
- **Actual:** Rows read '30 points . 100%', '26 points . 87%': percent is relative to the top answer, not survey share.
- **Impact:** Misleading.
- **Evidence:** poll/evidence/survey-showdown-chrome-1280x800-ERROR8-3.png
- **Console/network:** none
- **Suggested direction:** Drop the percent for points or label it.

### POLL-09 (Polish): Never Have I Ever (and built-in banks)
- **Category:** Content/copy issue
- **Environment:** all, all
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Play with built-in statements.
- **Expected:** Reunion and cell-group appropriate, no duplicated lead.
- **Actual:** Lead 'NEVER HAVE I EVER...' is followed by statements that begin 'Never have I ever...' (duplicated). Built-in banks (backend Games/Content/*.json) are child-oriented and Western: snowman, snowball fight, horse riding, 'Autumn/Spring', 'Wellies/Mac', 'pass the parcel'; content is clean and safe but not tuned for Uganda adults.
- **Impact:** Some prompts will be 'never' for everyone.
- **Evidence:** backend/RandomRoom.Api/Games/Content/never-have-i-ever.json,survey-showdown.json,would-you-rather.json
- **Console/network:** none
- **Suggested direction:** Strip the prefix from the bank or the lead; add local content (matooke, boda boda, etc.).

### POLL-10 (Polish): all five
- **Category:** UX issue
- **Environment:** Chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Host presses 'End round' mid-play, or 'Start new round' after finishing.
- **Expected:** Confirmation.
- **Actual:** Single click ends the game for everyone immediately ('Game over / Nobody scored'); Start new round resets scores with no warning. Scoreboards with no scores show every player as rank 1.
- **Impact:** Accidental taps.
- **Evidence:** poll/evidence/would-you-rather-chrome-1280x800-end-round-midplay-player.png
- **Console/network:** none
- **Suggested direction:** Add a confirm step; show '-' ranks before anyone scores.

### POLL-11 (Polish): all five
- **Category:** Performance
- **Environment:** Chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Throttled 400kbps/400ms, reload /room/<slug>.
- **Expected:** Early visible loading state.
- **Actual:** Blank body at 2.5s (200kbps); heading appears after 8.1s (WYR) to 15.5s (Survey), game UI 9.9s to 18.1s.
- **Impact:** Poor on slow connections.
- **Evidence:** poll/evidence/would-you-rather-chrome-1280x800-slow3g-midload.png,poll/checks-net-would-you-rather.json
- **Console/network:** n/a
- **Suggested direction:** Static HTML shell/skeleton; check lazy chunk sizes for game screens.

### QUIZ-07 (Polish): two-truths (shared screens)
- **Category:** Accessibility
- **Environment:** Chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Keyboard-only run of Two Truths as storyteller and voter (works end to end); inspect semantics.
- **Expected:** Heading order h1>h2>h3; focus kept after an action; one live announcement per change.
- **Actual:** Headings go h1 -> h3 (round card) -> h2 (Scoreboard). After voting the focused button becomes disabled and focus drops to body. Three separate sr-only role=status paragraphs announce round state. Game tiles' accessible name begins with 'Selected' once chosen. Keyboard operation, visible 3px focus outlines and Enter-to-submit all worked. axe: no violations in stable states; three color-contrast hits seen only mid-animation (stamp) in single runs, not confirmed.
- **Impact:** Minor screen-reader noise.
- **Evidence:** quiz/evidence/two-truths-chrome-keyboard-a11y.json
- **Console/network:** none
- **Suggested direction:** Use h2 for the round card, move focus to the confirmation text after voting.

### QUIZ-08 (Polish): all four
- **Category:** Content/copy
- **Environment:** Chrome, all
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Start a game, then Start new round after Game over.
- **Expected:** Consistent terms.
- **Actual:** Header shows 'Round 2 - Live' (session) while the card says 'Round 1 of 4' / 'Fact 1 of 4' (in-game round). The status pill after Game over reads 'Round 1 - Complete'. Also the storyteller's unsent statement draft is lost on refresh, and the Name That guess input has no inputmode/enterkeyhint.
- **Impact:** Users may confuse session round and game round.
- **Evidence:** quiz/evidence/two-truths-chrome-resilience.json
- **Console/network:** none
- **Suggested direction:** Rename the session label (e.g. 'Game 2') or the in-game label.

### REFLEX-04 (Polish): buzzer
- **Category:** UX issue
- **Environment:** chrome, firefox, brave, 1280x800, 390x844
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Two or more players buzz at once. Look at the losing player.
- **Expected:** Losing a race shows a neutral note, and clears.
- **Actual:** Loser gets a red error banner "Someone buzzed first." above the card, in addition to the "X buzzed first." status line, and it persists through later buzzes, resolution and the next round (confirmed in round 2).
- **Impact:** Looks like a failure and is redundant; stale banners confuse.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/reflex/evidence/buzzer-chrome-1280x800-raceA-loser.png; evidence/buzzer-chrome-1280x800-loser-banner-round2.png
- **Console/network:** 409 POST /api/room/action (expected)
- **Suggested direction:** Treat 409 race loss as non-error and clear error on any new snapshot.

### REFLEX-07 (Polish): spin-wheel
- **Category:** Accessibility
- **Environment:** chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Spin: result banner is a role=status live region that is sr-only until landing.
- **Expected:** Announcement only after the wheel stops.
- **Actual:** Code review only (not verified with a screen reader): the live-region text is populated as soon as the server result arrives, so assistive tech may announce it ~4s early.
- **Impact:** Spoiler for screen-reader users.
- **Evidence:** frontend/src/games/spin-wheel/GameScreen.tsx
- **Console/network:** n/a
- **Suggested direction:** Populate the live region only after landed.

### STORY-04 (Polish): fortunately
- **Category:** Content/copy issue
- **Environment:** all, all
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Open your turn in Fortunately/Unfortunately.
- **Expected:** A clear cue that you continue the sentence after the lead-in.
- **Actual:** The label reads 'Fortunately, What happens next?' (lead-in in colour followed by a capitalised question), which can be read as one odd sentence; the input itself has no placeholder.
- **Impact:** Low-confidence users may not realise the lead-in is added for them.
- **Evidence:** story/evidence/fortunately-chrome-1280x800-myturn.png
- **Console/network:** none
- **Suggested direction:** Show the lead-in inline in front of the input and use a plain label such as 'Finish the sentence'.

### STORY-05 (Polish): mad-libs, one-word-story, fortunately
- **Category:** UX issue
- **Environment:** all, all
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** In setup, untick the built-in set and leave no custom story/starter.
- **Expected:** Message says what is missing.
- **Actual:** The sticky bar says only 'Finish the setup to continue.' (Fill-in has a status line 'Add at least one story...' lower down).
- **Impact:** Minor friction for first-time hosts.
- **Evidence:** story/evidence/mad-libs-chrome-1280x800-setup-validation.png
- **Console/network:** none
- **Suggested direction:** Reuse the specific text ('Add at least one story or use the built-in ones') in the blocker message.

### WORD-07 (Polish): sketch-guess
- **Category:** UX
- **Environment:** chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Start a round as drawer on a 1280x800 window.
- **Expected:** Whole canvas visible.
- **Actual:** Canvas spans y=524-1100 (576 px), so the lower half and the tool row are below the fold; the timer is scrolled away while drawing.
- **Impact:** Drawer must scroll mid-round on a typical laptop.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/sketch-guess-chrome-1280x800-blank-drawer.png
- **Console/network:** none
- **Suggested direction:** Smaller max canvas or put tools/timer beside the canvas on wide screens.

### WORD-08 (Polish): sketch-guess
- **Category:** Polish
- **Environment:** chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Drag with the right mouse button on the canvas.
- **Expected:** Only the primary button draws.
- **Actual:** A right-button drag adds a stroke (onPointerDown has no button check).
- **Impact:** Accidental marks; context menu also opens.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/checks-sk-chrome.json
- **Console/network:** none
- **Suggested direction:** Ignore pointerdown unless event.button === 0 (or pointerType touch/pen).

### WORD-10 (Polish): forbidden-words, sketch-guess
- **Category:** UX (setup validation)
- **Environment:** chrome, 1280x900
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Forbidden Words: two custom cards 'Dog' and 'dog'. Sketch Guess: custom word '!!!'. Next, add players, Create the room.
- **Expected:** Setup step blocks Next with a message beside the field.
- **Actual:** Next is enabled; the error only appears after the players step as a line above the Back/Create buttons ('Each card needs a different word.' / 'Word 1 needs letters or numbers.', HTTP 400).
- **Impact:** Host has to go back two steps to find the problem.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/forbidden-words-chrome-1280x900-dupcards-create-error.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/sketch-guess-chrome-1280x900-punct-create-error.png,/tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/checks-setup2.json
- **Console/network:** 400 POST /api/rooms plain title
- **Suggested direction:** Mirror the server rules (duplicates, letters or numbers) in isSetupValid.

### WORD-11 (Polish): word-spies
- **Category:** Content/copy
- **Environment:** chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Finish a game with 2 winners on a team.
- **Expected:** Team win reads as a team result.
- **Actual:** Banner 'Game over: runP2 & runP3 tie' (ranks 1,1,3,3) under 'Red team wins'; teammates are shown as tied.
- **Impact:** Slightly confusing; the scoreboard cannot show the team result.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/word-spies-chrome-1280x800-complete-operative-runP1.png
- **Console/network:** none
- **Suggested direction:** Game over banner should name the winning team.

### WORD-12 (Polish): word-spies
- **Category:** Responsive/UI
- **Environment:** chrome, 390x844
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Open the board on a phone.
- **Expected:** Card words and owner labels are comfortably readable.
- **Actual:** Words are 0.7rem (~11 px) bold and owner labels 0.6rem (~9.6 px); cards are 56 px tall (44 px target met).
- **Impact:** Small text for low-vision users; the owner label is the non-colour cue.
- **Evidence:** /tmp/claude-1000/-home-amos-dev-dotnet-Choice-Maker/9b91db11-6bbe-441c-bf06-de62ac766934/scratchpad/word/evidence/word-spies-chrome-390x844-guessing-spymaster-wsm390P1.png
- **Console/network:** none
- **Suggested direction:** Raise to at least 0.8rem on phones or use a 4-column board.

### LEAD-02 (Polish): API (shared)
- **Category:** UX / Security (minor)
- **Environment:** Chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** POST /api/join more than 20 times in a minute.
- **Expected:** A throttled response that tells clients when to retry.
- **Actual:** 429 with an empty body and no Retry-After header. The join screen does show 'Too many attempts. Wait a minute and try again.'
- **Impact:** Clients other than this UI get no hint how long to wait.
- **Evidence:** scratchpad/ratelimit.mjs output, scratchpad/runs/ratelimit/
- **Console/network:** 429 on /api/join after 20 requests
- **Suggested direction:** Return Retry-After and a short problem+json body.

### LEAD-03 (Polish): All (dev only)
- **Category:** Bug (dev build)
- **Environment:** Chrome, 1280x800
- **Verified by lead:** not independently re-run (Minor/Polish; the spec requires lead re-verification only for Major and Blocker)
- **Steps:** Open any page with `npm run dev` and read the console.
- **Expected:** No console errors.
- **Actual:** 'Failed to start the connection: The connection was stopped during negotiation' on page load in dev. Not seen in the production build (non-debug console empty on desktop and mobile runs).
- **Impact:** Noise in dev only; likely a double mount of the SignalR start/stop.
- **Evidence:** scratchpad/runs/smoke-rp2/final-smokerP1.log.json, scratchpad/runs/prod-desktop/
- **Console/network:** see evidence
- **Suggested direction:** Guard hub start/stop against the double effect run, or ignore the negotiation abort.


## 6. Cross-Game Patterns

1. **Stale snapshot overwrite (RoomScreen/useRoom).** POLL-01, QUIZ-01, REFLEX-03, STORY-01, WORD-02 are one root cause. Turn-based games (One-Word, Fortunately) did not show it; simultaneous-action games did, more often at 8 players.
2. **Reveal/Game-over stamp animation widens the page in mobile emulation** (QUIZ-04, STORY-02, WORD-05). Shared `animate-stamp` (scale 1.6). Not reproduced with reduced motion or at 320px desktop. Real-device behaviour unverified.
3. **Unbounded input returns a 500** (POLL-02, QUIZ-06, REFLEX-06): player names over 32 characters (the form allows 40), long titles and long choices.
4. **"Round" used for two things** (POLL-06, QUIZ-08): the session and the in-game round.
5. **No confirmation on End round** (POLL-10, WORD-09). One click ends the game for everyone.
6. **Setup errors appear only on the last step** (POLL-07, REFLEX-05, STORY-05, WORD-10).
7. **Stale error alerts persist into the next round** (POLL-03, QUIZ-03, REFLEX-04).

## 7. Accessibility Findings

- **axe-core (WCAG 2.0/2.1/2.2 A and AA tags):** zero violations on every stable state scanned: 419 scans (poll), 23 runs (story), and every checked state in quiz, reflex and word. Quiz saw 3 colour-contrast hits in single runs mid-animation that were not confirmed.
- **Contrast:** `npm run check:contrast` passes, all 44 pairings.
- **Keyboard:** keyboard-only play passed for the poll games and Two Truths with visible focus. Not run for the reflex, story and word games.
- **Semantics (QUIZ-07):** heading order h1 to h3 to h2, focus lost after voting, duplicate live regions.
- **Reduced motion:** passed on the games tested; the wheel result is hidden mid-spin only with reduced motion (REFLEX-01).
- **Possible (unverified):** REFLEX-07, the wheel's live region may announce the result before the wheel stops.
- **Known gap:** Sketch Guess has no keyboard equivalent, so a keyboard or screen-reader user cannot draw.
- **Not tested:** OS-level large text, 200% and 400% browser zoom (320px reflow checked for some games only).

## 8. Performance Findings

Lighthouse mobile preset, throttling on, production build via vite preview:

| Page | Perf | LCP | CLS | TBT | JS transferred |
| --- | --- | --- | --- | --- | --- |
| Home | 95 | 2.4 s | 0 | 0 ms | 101 KB |
| Room join | 94 | 2.8 s | 0 | 10 ms | 101 KB |

- Main bundle 347.6 kB raw, 102.6 kB gzip (under the 150 KB target). Each game's screen and setup are separate lazy chunks of 1 to 9 kB gzip.
- INP was not measured (Lighthouse navigation mode does not produce it).
- Game picker and in-room game screens were **not** measured with Lighthouse (they sit behind a session).
- Slow 3G reload (poll agent, Would You Rather and Survey Showdown): blank page for 8 to 15 seconds before the heading appears (POLL-11).
- Trivia heap grew from 19 MB to 55 MB over 20 questions in one run; no conclusion drawn.
- Production build: tsc and vite build succeed; SPA fallback returns 200 on a deep link; no service worker; vite preview served assets with Cache-Control: no-cache (production caching depends on the real host, not tested).
- Production functional check: a 4-player Random Picker round on the preview, desktop and mobile emulation, showed the stale-screen bug (REFLEX-03 pattern) in 1 of 2 runs and no console errors.

## 9. Security / Information-Leak Findings

**Confirmed issue**
- **QUIZ-02:** Trivia sends other players' correct/incorrect flags while a question is open.
- **Minor:** POLL-04 (Back lands on a used invite page).

**Attempted tests that passed**
- Hidden information in WebSocket frames, DOM and refresh/rejoin states: Word Spies key (operatives never received it), Forbidden Words card (guessers never), Sketch Guess word (drawer only), Bingo (each client only its own card), Fill-in Stories text (not before the read-out), Two Truths lie (never before reveal), Guess Who authorship, Name That accepted answers, Trivia correctIndex while a question is open, poll answers before reveal.
- Non-host host-only actions return 403; no or forged token returns 401; out-of-phase 409; bad input 400; none with stack traces.
- Double answers, double Next, double Buzz, simultaneous Bingo claims all resolve to one authoritative result.
- Expired token (a correctly signed token with a past expiry, signed in-process with the .env key, never printed): API 401, UI returns to the join screen and clears the stored session.
- Rate limiting: join returns 429 after 20 per minute; the join screen says "Too many attempts. Wait a minute and try again." (LEAD-02 for the missing Retry-After).
- No PIN in localStorage or URLs. The session token is stored in localStorage (random-room-session), and the SignalR access_token is in the WebSocket URL query (normal for SignalR, noted for awareness).

**Could not be performed**
- Replaying captured requests beyond double-submit, acting as another player with a forged valid token (no key use for that), and any penetration-style testing, by design.

## 10. Missing Features / Proposals

- No spectator role: every viewer is a signed-in player, so the "spectator view" in the spec could not be tested.
- The game picker has category headings and search but no category filter (the spec asks for filters). Search does not match category names (QUIZ-05).
- No confirmation step before End round.
- The host gets no hint which seat is unclaimed or which player has not answered (POLL-05).
- A Back-button guard or "leave game?" prompt during active games (POLL-04).
- Better invite flows and PIN recovery are already in G1 and G2.

## 11. Not Tested / Blocked

| Area | Why |
| --- | --- |
| WebKit (all 19 games) | BLOCKED. Playwright WebKit installs but cannot launch: libavif16 and libmanette-0.2-0 are missing and need sudo to install. |
| Spectator view | BLOCKED. The app has no spectator role. |
| Real device / iOS Safari behaviour, keyboard covering inputs, safe areas | Not testable with emulation. Mobile emulation findings (QUIZ-04, STORY-02, WORD-05) may be emulation-only. |
| OS large text | Cannot be simulated here. |
| Phone lock / background freeze | CDP freeze was inconclusive in the poll run; not counted as covered. |
| Slow 3G and offline | Poll agent: Would You Rather and Survey Showdown only. Lead: offline mid-round on Would You Rather. Other games not run. |
| 200% and 400% zoom, 1920 to 320 live resize, 1920/1440/1024/768 width sweep | Done only partially (320px for some games). Not systematically per game. |
| Keyboard-only runs | Poll games and Two Truths only. |
| Lighthouse on picker and game screens; INP | Only home and join measured. |
| Gaps_Bugs: migrations on startup, stale server, leftover strokes | Not exercised. |
| Server restart | Done once (Would You Rather, 4 players). See section 13. |
| Long Sketch Guess session, memory/CPU growth | Not run beyond the Trivia heap observation. |

## 12. Documentation Discrepancies

- README "Auth model" says each of four players has a PIN set in .env. Actual: the host creates a room and names the players, each player sets their own PIN through an invite link, nothing comes from .env.
- README deploy section tells you to set Room__PlayerPins__* and Room__HostPlayer, and .env.example lists Room__PlayerPins__Amos/Lydia/James/Jacob; the player-PIN model is no longer used.
- README says to open /room/who-do-we-choose; rooms now have generated slugs.
- README says "four players"; the app supports 2 to 12 depending on the game.

## 13. Self-Audit

**Covered:** a real multi-player lifecycle (create, claim, join, start, play, reveal, finish, restart) for all 19 games on Chrome with 4 players and 8-player runs, Firefox and Brave smoke rounds, three emulated viewports, axe on every stable state, hidden-information scans of WebSocket frames for each role, authorization probes, race attempts, rate limit, token expiry, offline, one server restart, contrast, production build, and Lighthouse on two pages.

**Not covered:** see section 11. WebKit is the largest gap.

**Server restart (lead):** with 2 of 4 answers in, the API was stopped and restarted (about 6 seconds down; the browser saw 502 meanwhile). Afterwards all four clients showed the same phase, "2 of 4 answered", the same players locked in, no duplicate player names. After the last two answered, only 1 of 4 clients showed results within 3 seconds. I could not tell whether that is the stale-screen bug or missed pushes after reconnect, so it is **unconfirmed** and not listed as a finding.

**Assumptions and limits:**
- Findings other than the verified Majors come from the category agents' scripts and logs; I did not re-run them. Each finding lists its evidence so it can be checked.
- Some agent script checks failed because of script timing and were excluded (the agents noted them).
- The host network was unstable for part of the run. The stale-screen bug was re-verified on a normal setup, but resilience and timing numbers from affected runs should be read with that in mind.
- Mobile "viewport zoomed out" findings come from emulation and are not verified on a real phone.
- Confidence is lower for: REFLEX-02 (code and agent evidence only), WORD-02 and STORY-01 (root cause confirmed in another game, not re-run in those), and everything measured under the flapping network.

## 14. Status after fixes

Written after the audit, from the commits on `feat/playground-redesign-and-games`. "Verified" means checked again after the fix:
a failing test that passes, plus a real-browser or live-API run where stated. Everything not listed here is still open.

| Finding | Status | Commit |
| --- | --- | --- |
| POLL-01, QUIZ-01, REFLEX-03, STORY-01, WORD-02 (stale screen) | Fixed. Verified in a browser on Would You Rather only (8 players, 24 rounds, 0 stale; the old client left clients stuck) | 5ae05d0, b8ea3b3, a42bf7c |
| QUIZ-02 (Trivia leak) | Fixed. Verified against the live API | 1cb7873 |
| REFLEX-01 (wheel result early) | Fixed. Verified in a browser | 1cb3718 |
| REFLEX-02 (Bingo overflow) | Fixed. Verified in a browser (24 of 25 cells overflowed before, 0 after, at 390 and 320 px) | 327a0ff |
| POLL-02, REFLEX-06, QUIZ-06 (oversized input gives 500) | Fixed for names, titles, picker choices and trivia question text. Not done: caps on trivia question and option counts and option length (needs a product decision) | 64f3f13, 4c09faa |
| QUIZ-04, STORY-02, WORD-05 (page widens after reveal) | Fixed in mobile emulation (390 to 465 px before, 390 after). Not checked on a real phone | da45708 |
| STORY-03 (long unbroken story text) | Fixed. Verified in a browser | 01c3137 |
| POLL-03, REFLEX-04, QUIZ-03 (stale errors, tick errors) | Fixed. Unit tests only, not re-run in a browser | 5e39c24 |
| WORD-01 (wrong team named) | Fixed | 627a95d |
| QUIZ-05 (search ignores category) | Fixed | b592c4c |
| REFLEX-05 (duplicate picker choices) | Fixed | 4f6016d |
| WORD-09, POLL-10 (no confirm on End) | Fixed with a reusable confirm. The "every player shows rank 1 before anyone scores" part of POLL-10 is not done | 95c54d6 |
| POLL-06, QUIZ-08 (Round means two things) | Mostly fixed: the game is now "Game N", rounds are for prompts. Not done: unsent draft lost on refresh, Name That input hints | c5c98c0 |
| POLL-04 (Back lands on used invite) | Fixed. Verified in a browser | 5155740 |
| POLL-05 (unclaimed seat not shown) | Fixed. Verified in a browser | b6cc77b, bf43f46 |
| WORD-03, WORD-04, WORD-08 (Sketch lag, rejected strokes, right-click) | Fixed. WORD-03 verified in a browser (guesser ink grows during the drag) | 60614cc |
| WORD-06 (canvas too tall in landscape) | Fixed. Verified in a browser (576 px to 273 px at 844x390) | 7605bf7 |
| REFLEX-07 (wheel announced early) | Fixed. Unit test; not tried with a screen reader | 30a5977 |
| LEAD-02 (429 without Retry-After) | Fixed. Verified through the dev proxy | 06ae386 |
| POLL-08 (misleading survey percent) | Fixed | d3a2e8d |
| WORD-12 (tiny Word Spies text) | Fixed. Checked in a browser at 320 and 360 px. The larger owner label at 390 px and up was not looked at | 2a72622 |
| POLL-07, STORY-05, WORD-10 (setup errors only on the last step) | Fixed: the setup bar now says what is wrong, mirroring the server's rules (duplicate Forbidden Words cards, Sketch Guess words with no letters, unfinished dilemmas, no story starter, duplicate picker choices). Component and module tests; not re-run in a browser | 4a1cf7a |
| POLL-11 (slow-3G blank page) | The audit's 8 to 15 s did not reproduce on the production build (skeleton 3.4 s, game 5.0 s at 400kbps and 400ms). Still improved: a static loading line takes first paint from 3.3 s to 1.3 s with layout shift unchanged at 0 | fccba58 |
| LEAD-01 (join page LCP 2.8 s) | Not reproduced: 1.9 s in four Lighthouse runs on the production build. The audit run was probably distorted by the flapping network. No change made | |
| STORY-04 (Fortunately label) | Fixed | 5d138dd |
| POLL-10, the rank part (everyone shown as rank 1 at zero) | Fixed | 5a40273 |
| WORD-11 (team win shown as a tie) | Fixed | 4985107 |
| POLL-09 (duplicated lead-in) | Duplicate lead-in fixed. Not done: local, adult-appropriate content for the built-in banks (a content decision) | d664d34 |
| QUIZ-07 (Two Truths accessibility) | Heading level fixed. Not done: focus after voting, the three live regions | 451ca9e |
| LEAD-04 (build empties wwwroot) | Documented, not changed: the folder is git-ignored build output the Dockerfile relies on | 61a9005 |
| Documentation discrepancies (section 12) | Fixed | 61a9005 |

**Still open:** WORD-07 (Sketch canvas below the fold on a laptop), LEAD-03 (dev-only console error from React StrictMode; the fix touches the realtime connection teardown, so it was left alone), the unfixed parts noted above, caps on trivia question and option counts (QUIZ-06), and the Known Issues G1 and G2.
