# Gaps and Bugs

Known problems and missing features, found while testing the redesigned app locally.
Nothing here is fixed yet. Each entry says what was seen, what is not yet known, and what "done" looks like.

## Bugs

### B1. Cannot get from the join screen into the game

- **Seen:** On `/room/<code>/join` (Most Likely To room, 3 players) the header badge reads "3 OF 3 PLAYERS READY", but the player cannot proceed to the actual game. No player is selected, the PIN field is empty, and "Enter the room" is greyed out.
- **Expected:** Once everyone is ready, each player can pick their name, enter their PIN and reach the game screen, and the host can start the game.
- **Not yet known:**
  - Whether the join flow is meant to be entered here at all, or whether players should arrive through their private invite link and set a PIN first.
  - Which PIN a player is expected to type on this screen, and whether it is explained anywhere.
  - Whether a "ready" state is meant to trigger something (auto-advance, host "Start" button) that never happens.
- **To investigate:** Reproduce from room creation through invite claim to join, watching the network calls. Check what "ready" means in `JoinScreen`, `ClaimInviteScreen` and the room snapshot, and what unlocks the button.
- **Done when:** A fresh room can be created, all players can join, and the game screen is reachable, with a test covering the path.

### B2. Search icon renders huge in the game picker (fixed locally, not committed)

- **Seen:** The magnifier in the picker's search box filled the page.
- **Cause:** `Icon` in `frontend/src/components/icons.tsx` spread the caller's `className` over its default `size-5`, so the SVG had no size.
- **Status:** Fixed in the working tree (merge the classes instead of replacing them). Needs a regression test and its own commit.

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

## Other known gaps

- **Migrations run on startup against whatever database is configured.** A `.env` pointing at a production database will be migrated just by running the app locally. Consider a safeguard, such as refusing to auto-migrate outside Development unless an explicit setting is on.
- **Stale local server.** An old backend process kept port 5184 and served an out-of-date build, which produced confusing "unknown game type" errors. Worth documenting the dev workflow in the README.
- **Commit authorship.** Three early commits on this branch are attributed to the wrong identity and need re-authoring before merge.
- **Sketch Guess has no keyboard equivalent** for drawing (pointer and touch only).
- **Buzzer fairness** depends on which connection reaches the server first.
- **Leftover drawing strokes** stay in the database if a session is ended abnormally, until the room is deleted.
