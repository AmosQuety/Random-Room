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
| S3 | Room-created card: invite link per seat, host recovery code shown once, retention notice shown | not run |
| S4 | Host sets a PIN; players Ann and Ben claim their invites and sign in | not run |
| S5 | Host's room screen: marked as referee, "You are refereeing" line, no answer controls, host controls present | not run |
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
