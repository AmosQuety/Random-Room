# Production smoke test, 2026-11-01

Target: https://random-room.onrender.com (Render free instance, Neon Postgres, items 1 to 5 deployed).
Method: Android emulator `Medium_Phone_API_37.0` driven through Argent for the host; a second actor for the players.
Test data only: room title "SMOKE TEST (delete me)", fake names (Smoke, Ann, Ben), fake PINs. No invite links, tokens or
recovery codes are written in this file.

This file is the source of truth for resuming after any interruption. Rule: after EVERY step, append the result here
at once (PASS / FAIL / NOTE, with what was seen), then flush to disk. Never redo a step already marked PASS.

## Checklist

| Step | What is checked | Result |
|---|---|---|
| S1 | Site loads on the phone; time of the first load noted (cold start); no horizontal scroll | PASS |
| S2 | Create flow: pick Would You Rather; Players step says the host is the referee and shows participants against the game's range; "I want to play too" is off | PASS |
| S3 | Room-created card: invite link per seat, host recovery code shown once, retention notice shown | PASS content, FAIL minor layout |
| S4 | Host sets a PIN; players Ann and Ben claim their invites and sign in | PASS |
| S5 | Host's room screen: marked as referee, "You are refereeing" line, no answer controls, host controls present | PASS |
| S6 | Players' screens: normal answer controls; scoreboard lists only Ann and Ben | not run |
| S7 | Play Would You Rather through: answer, reveal, next, finish; host never on the scoreboard | not run |
| S8 | Reload the host page mid-game: state restored | not run |
| S9 | Game finished: scoreboard excludes the host; host sees "Play in the next game" with its explanation | not run |
| S10 | Turn the toggle on: the FINISHED game's scoreboard does not change; then "Start new game" | not run |
| S11 | New game: host now plays (answer controls, on the scoreboard) | not run |
| S12 | Buzzer: the toggle is disabled with a reason for a game that needs a referee (or note why not testable) | not run |
| S13 | Host deletes the test room; players see "The host deleted this room"; the room no longer loads | not run |
| S14 | Layout at phone size on each screen visited: readable, nothing cut off; note anything confusing in plain words | not run |

## Findings log (newest at the bottom)

- 18:39 S1 PASS: Site loaded in Chrome on the Medium_Phone_API_37.0 emulator within a few seconds (instance was warm: curl from desktop returned 200 in 1.2s). Landing page 'The Playground' (START A GAME / JOIN WITH A CODE) fits phone width, no horizontal scroll seen. Note: first load was not a cold start.
- 18:41 S2 NOTE: Test tooling, not the app: Argent keyboard typing on the emulator lost characters (typed 'SMOKE TEST (delete me)', field showed 'SSM') because Gboard stylus-handwriting popups intercepted input. Disabled stylus handwriting on the emulator via adb and typed with 'adb shell input text'; the room title field then shows exactly 'SMOKE TEST (delete me)'. Setup screen 'Set up Would You Rather' looks fine (Room name, built-in dilemmas 30, Rounds 8, Time per round No limit).
- 18:42 S2 NOTE: Players step seen (before I lost the wizard by pressing Android Back, which closed Chrome; my own mistake, no room was created). Copy on screen: 'You are the host and referee. Add the people who will play. Each person gets their own private invite link.'; 'Rooms are deleted after 30 days without play. The host can delete a room sooner.'; field hint 'Type your own name first, then everyone joining the game.' (fields 'Player 1' 'Player 2', first is the host); counter '0 players so far (not counting the host). Would You Rather takes 2-12 players.'; checkbox 'I want to play too' present, unticked; blocker 'Would You Rather needs at least 2 players. Add 1 more.' (updates as names are typed). Mild confusion: with Smoke + Ann typed it says 'Add 1 more' - correct, as host is excluded. Restarting the wizard.
- 18:44 S2 PASS: Create flow on phone: Would You Rather picked, title 'SMOKE TEST (delete me)', Players step says 'You are the host and referee...'; counter '2 players so far (not counting the host). Would You Rather takes 2-12 players.' with Smoke, Ann, Ben; 'I want to play too' unticked with hint 'Off: the host runs the game and does not play. You can change this between games.'; 'WHO'S HOSTING?' select defaults to Smoke ('The host starts and ends games and referees, so does not play. Defaults to the first name.'). Typing via adb input text, not Argent keyboard.
- 18:45 S3 NOTE: Room created. Card 'Send each player their own link' with a card per seat (Smoke marked YOU / HOST, Ann, Ben), each with SHARE, COPY LINK, SHOW CODE; Smoke also 'SET MY PIN'. LAYOUT FINDING (minor to major, phone width 412): the seat name is overlapped and cut by the action buttons: 'Smoke' shows as 'Smok' under the red SET MY PIN / SHARE buttons, 'Ann' and 'Ben' are partly covered by SHARE, and the link text shows only 'h...' / 'ht...'. Needs a second look after scrolling to confirm it is not a screenshot-time animation. Recovery code card ('Your recovery code') is below.
- 18:45 S3 FAIL: (minor, layout, confirmed after scrolling) On the room-created card the seat name and link text get squeezed to a sliver (about 7 percent of the width) by the buttons beside them: 'Ann' and 'Ben' are partly covered by the SHARE button and the link shows only 'ht...'; 'Smoke' is cut to 'Smok' under SET MY PIN / SHARE. Names are hard to read; the buttons should wrap below the name.
- 18:45 S3 PASS: Per-seat invite cards present (Smoke/Ann/Ben, SHARE / COPY LINK / SHOW CODE), host recovery code card shown ('It is shown only once, so save it somewhere safe...'), 'COPY ALL LINKS', 'ALREADY SET A PIN? GO TO THE ROOM', and retention notice 'Rooms are deleted after 30 days without play. The host can delete a room sooner.' all visible. Content passes; layout issue logged above.
- 18:46 S4 NOTE: Host part: 'Set your PIN' page (hint 'At least 4 characters.'), PIN 1111 set on the phone, landed straight in the room as 'You are Smoke (host)'. Chrome then offered 'Save password? To Google Password Manager' for the PIN field (browser behaviour; a PIN field offered to a password manager is worth a look). Players Ann and Ben: still to claim (via curl, see next lines).
- 18:46 S5 PASS: Host room screen: header 'WOULD YOU RATHER' badge 'GAME 1 . WAITING', title 'SMOKE TEST (delete me)', 'You are Smoke (host)'. Pink box 'YOU ARE REFEREEING - You run the game and do not play. Reveal the answers when you are ready, then move to the next round.' Panel 'READY WHEN YOU ARE / 8 rounds / Press Start game below when everyone is here.' Scoreboard lists only Ann 0 and Ben 0 (no host). Start game button below. No answer controls.
- 18:46 S3 FAIL (minor, reported by the owner from a screenshot of the emulator; worker to verify and add detail): on the room-created card 'Send each player their own link', at phone width the host's row is broken. The name 'Smoke' and the 'YOU' and 'HOST' badges are overlapped and partly hidden behind the 'SET MY PIN' and 'SHARE' buttons, and the link preview under the name is cut to 'h...'. Ann's and Ben's rows are cramped too: the name touches the 'SHARE' button and the preview shows only 'ht...'. It is the first thing a host sees after creating a room, so it is worth fixing.
- 18:46 S3 NOTE: Follow-up measurements (verified on the emulator, from the element tree of the card): the name text box is only 0.056 to 0.070 of the screen width (about 23 to 29 px of a 412 px wide window, so well under the 'Smoke' text width), and the link-preview text box has the same width, so it truncates to 'h...'. The SHARE button (0.27 of width) and SET MY PIN (0.35) sit in the same row and are drawn over the name; the row cannot wrap them below. Confirmed for all three seats, not only the host. NOT tested: longer or shorter host names, rotation, zoom (the card is shown only once after creation and I may create only one room, so it cannot be reopened); suggest the owner or a follow-up run checks these.
- 18:46 S4 PASS: Host set PIN 1111 on the phone (S4 part 1). Ann and Ben claimed their invites via curl POST /api/rooms/{slug}/claim/{token} with pin 1111: both returned 200 with their player names ({"player":"Ann"}, {"player":"Ben"}). Method: host on emulator UI, Ann and Ben through the API with cookie jars (not the UI).
