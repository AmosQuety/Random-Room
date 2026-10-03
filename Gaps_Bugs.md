# Gaps and Bugs

Known problems and missing features, found while testing the redesigned app locally.
Each entry says what was seen, what is not yet known, and what "done" looks like.

## Bugs

### B1. Cannot get past the join screen into the game (fixed)

- **Cause:** Not a backend fault. The join form is working as designed (pick a name, type the PIN you set), but the flow gave no guidance. After setting a PIN the player landed on a blank join form with a disabled button and no explanation, and the host's own seat was unclaimed with nothing pointing them to their link. "3 of 3 players ready" only meant the seats were claimed, not that this browser was signed in.
- **Fix:** Claiming an invite now signs the player straight in with the PIN they just set and opens the room (the manual join form remains as a fallback). The join screen says what is still missing while the button is disabled. The room-created card marks the host's row "You, host" with a "Set my PIN" button.
- **Verified:** Component tests for both screens, plus a real Chrome run at 390px wide: create room, open invite, set PIN, land in the room.
- **Still open:** Forgotten-PIN recovery, tracked as G2.

### B2. Search icon renders huge in the game picker (fixed)

- **Seen:** The magnifier in the picker's search box filled the page.
- **Cause:** `Icon` in `frontend/src/components/icons.tsx` spread the caller's `className` over its default `size-5`, so the SVG had no size.
- **Status:** Fixed by merging the caller's classes with the default size instead of replacing it. A regression test for icon sizing is still worth adding.

## Gaps

### G1. Better ways to invite players (partly done)

**Done:** per-player Share (Web Share API, so a tap opens WhatsApp and the like), Copy link, and a QR code drawn in the browser (items 1, 2 and 6 below; the re-send is the seat panel's "Get a new link"). **Still open:** the host lobby view (item 4) and the room-code display (item 5). Item 3 (one shared "pick your name" link) was decided against: anyone with the link could claim any unclaimed seat.

Original list:

Today the host has to send each person a private link by hand. Suggestions, roughly in order of value for effort:

1. **Copy-link and Share buttons per player.** Use the Web Share API (`navigator.share`) so the phone's share sheet opens straight into WhatsApp, SMS or email, with a plain copy-link fallback on desktop. Most groups already live in WhatsApp, so this may matter more than anything else here.
2. **QR code per player, and one for the room.** Shown on the host's screen so people in the same room can scan it. Generate it in the browser so no invite link is sent to a third party. This needs either a small QR library or a small self-written encoder, so decide by bundle cost (dependency rule: justify any new package).
3. **"Pick your name" room link.** One shared link where players choose their own name from the list and enter a PIN (this is roughly what the join screen does today), so the host sends one message instead of one per person. This depends on B1 being fixed.
4. **Host lobby view.** A live list showing who has opened their link, who has claimed a name and who is connected, with a "Start" button that enables when enough players are in.
5. **Short room code entry.** The home screen already accepts a room code, so display the code prominently and make it easy to read aloud.
6. **Reminder or re-send.** Let the host re-copy a link for anyone who lost theirs.

Security notes to keep in mind:

- A per-player invite link works like a password for that seat. Show it only to the host, never in logs, analytics or the public room preview.
- Consider link expiry, one-time claim and rate limiting on PIN attempts.
- QR codes for per-player links need to be visible only on the host's device.

Low-bandwidth note: keep whatever is added small (no image assets, no heavy libraries), since many players will be on slow connections.

### G2. No way to recover a forgotten PIN (fixed)

- **Fix:** The host can reset any other seat between games ("Someone locked out? Reset a seat"). The reset empties the seat, signs the old device out at once (a per-seat token version is checked on every request, and live connections are cut), and returns a fresh one-time link. It is recorded in `RoomAuditEvents`. The host's own seat cannot be reset: if the host forgets their PIN, they start a new room.
- **Why between games only:** a mid-game reset would let the host claim the seat and see that player's secrets (their cards, the spymaster key).
- **Still open:** a recovery path for the host's own seat (for example a code shown once at room creation).

## Findings from the QA audit

The first full QA audit is in `QA_REPORT.md` (47 findings, no blockers). All 8 Major findings and nearly all Minor and Polish ones are fixed; section 14 of the report lists every finding's status and the commit that fixed it. Still open: the Sketch Guess canvas below the fold on a laptop (WORD-07), local content for the built-in banks (POLL-09), focus after voting in Two Truths (QUIZ-07), a dev-only console message (LEAD-03), and caps on trivia question and option counts (QUIZ-06, needs a decision).

## Other known gaps

- **Migrations run on startup against whatever database is configured.** A `.env` pointing at a production database will be migrated just by running the app locally. Consider a safeguard, such as refusing to auto-migrate outside Development unless an explicit setting is on.
- **Stale local server.** An old backend process kept port 5184 and served an out-of-date build, which produced confusing "unknown game type" errors. Worth documenting the dev workflow in the README.
- **Sketch Guess has no keyboard equivalent** for drawing (pointer and touch only).
- **Buzzer fairness** depends on which connection reaches the server first.
- **Leftover drawing strokes** stay in the database if a session is ended abnormally, until the room is deleted.
