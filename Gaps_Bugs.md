# Gaps and Bugs

Known problems and missing features, found while testing the redesigned app locally.
Nothing here is fixed yet. Each entry says what was seen, what is not yet known, and what "done" looks like.

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

### G1. Better ways to invite players

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

### G2. No way to recover a forgotten PIN

- **Seen:** A player's PIN is set once, through their invite link, and only a hash is stored. The invite token is cleared on claim, so the link cannot be reused. If a player forgets their PIN, or opens the room on a new device without remembering it, they are locked out of that seat. The host cannot see or reset it, by design.
- **Why it matters:** Players are casual users (reunions, cell groups) and a four-digit PIN set once is easy to forget. Today the only recovery is to create a new room.
- **Not yet known:** Whether the host should be allowed to re-open a seat, and how that is kept safe from a host impersonating a player.
- **Proposed direction (needs a decision):** A host-only "Reset this seat" action that clears the seat's PIN hash and issues a fresh one-time invite link. The player opens it and sets a new PIN, exactly as at first join. Options to weigh:
  1. Host resets any non-host seat (simplest; the host could then claim that seat, so the reset should be visible to everyone in the room and recorded in an audit entry).
  2. Allow it only while no session is running, to avoid mid-round takeovers.
  3. Keep the PIN unrecoverable and just document it, accepting new-room as the recovery path.
- **Security notes:** Rate-limit the reset endpoint, expire the new invite, never return the old PIN hash, and make sure resetting the host's own seat is handled (probably not allowed).
- **Done when:** A host can issue a replacement link for a locked-out player, the old PIN stops working, the event is visible in the room and audited, and a test covers the reset and the lockout of the old PIN.

## Findings from the QA audit

The first full QA audit is in `QA_REPORT.md` (47 findings, no blockers). All 8 Major findings are fixed, along with most Minors; section 14 of the report lists every finding's status and the commit that fixed it. Still open: setup errors shown only on the last step (POLL-07, STORY-05, WORD-10), slow-3G first paint (POLL-11), the join page LCP (LEAD-01), a few Polish items, and caps on trivia question and option counts.

## Other known gaps

- **Migrations run on startup against whatever database is configured.** A `.env` pointing at a production database will be migrated just by running the app locally. Consider a safeguard, such as refusing to auto-migrate outside Development unless an explicit setting is on.
- **Stale local server.** An old backend process kept port 5184 and served an out-of-date build, which produced confusing "unknown game type" errors. Worth documenting the dev workflow in the README.
- **Sketch Guess has no keyboard equivalent** for drawing (pointer and touch only).
- **Buzzer fairness** depends on which connection reaches the server first.
- **Leftover drawing strokes** stay in the database if a session is ended abnormally, until the room is deleted.
