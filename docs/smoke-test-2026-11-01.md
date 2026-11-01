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
| S1 | Site loads on the phone; time of the first load noted (cold start); no horizontal scroll | not run |
| S2 | Create flow: pick Would You Rather; Players step says the host is the referee and shows participants against the game's range; "I want to play too" is off | not run |
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

