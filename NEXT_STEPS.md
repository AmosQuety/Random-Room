# Next steps

Written 2026-10-03 at the end of the second QA round. Read this first when you come back.

## Where things stand

- **Branch:** `feat/playground-redesign-and-games`. It is 74 commits ahead of `main` and `main` has nothing the branch lacks, so a merge is a plain fast-forward. **12 of those commits are not pushed yet.** Every commit is authored as you.
- **Built:** 19 games, all registered plugins with backend and frontend tests. Per-player snapshots keep hidden information (spymaster key, drawer's word, Bingo cards, the lie) out of other players' payloads. The invite and PIN model works end to end.
- **Tests, last run:** backend 483 passing against a local Postgres on port 5433; frontend 281 passing, plus lint (one old warning in `useRoom.ts`), `tsc -b`, `npm run check:contrast` and `npm run build`.
- **QA:** two full audits (`QA_REPORT.md`, `QA_REPORT_2.md`, prompts in `docs/`). The first found 8 Majors, all fixed and confirmed. The second found 1 Major and 14 smaller items, all fixed and committed. `QA_REPORT_2.md` is still untracked: decide whether to commit it.
- **Checked against the real API and Chrome after the fixes:** the End-game-versus-answer race (old code 11 of 12 stale, fixed code 0 of 12), the stroke caps, two-finger touch, sign-in expiry, Bingo, Word Spies layout, the no-JavaScript message and the favicon.

## First things to do (small, in this order)

1. **Push the branch** (`git push`) and open a PR into `main`. CI (`.github/workflows/ci.yml`) runs on pull requests only, so the PR is the first time it runs. Fix anything it finds.
2. **Commit or discard `QA_REPORT_2.md`.** Then update the stale docs below.
3. **Refresh stale docs:**
   - `Gaps_Bugs.md` says "Nothing here is fixed yet" and still lists B1 and B2 as open-ish. B1 and B2 are fixed; the QA section needs the second audit's result.
   - `PROGRESS.md` has an "Open item" about rewriting three commits' author. That is no longer true: all commits are yours. Remove it.
   - `QA_REPORT.md` section 14 is the status table for audit 1; add a short pointer to `QA_REPORT_2.md`.

## To build (in this order; remove each when done)

Done so far in this round: PIN recovery (host-only seat reset, `Gaps_Bugs.md` G2) better invites (Share, Copy, QR code; G1 items 1, 2 and 6) and trivia size limits. Still open from them: a recovery path for the host's own seat, a host lobby view (G1 item 4), and a shared "pick your name" link, which I decided against because anyone with it could claim any unclaimed seat.

### 1. Local trivia content
A draft of 20 East Africa questions is in `docs/trivia-east-africa-draft.md`, **not shipped**. Review every answer, swap in questions your players know, then add it as a second bank and a "pack" choice on the Trivia setup. (The caps part is done: 6 options, 100 characters per option, 50 questions per room.)

### 2. Delete inactive rooms
Daily background job removes rooms with no activity for `Room:RetentionDays` (default 30, 0 turns it off). Keeps free-tier databases from filling.

## Decisions waiting for you

| Item | Why it matters | Options |
|---|---|---|
| **Migrations run on every startup** (`Program.cs`) | A `.env` pointing at a production database gets migrated just by running the app locally. Not disruptive to the schema (all migrations are additive); the risk is the wrong database. | Auto-migrate only when an explicit setting is on (set it in production). |
| **Which database service** | The README says Render's free Postgres expires after 30 days. | Render Postgres paid (simplest next to the API), Neon free (cheapest, sleeps when idle), Supabase. Put the API and database in the same region. Change `SslMode.Prefer` to `Require` for any database over the internet. Check current prices first. |
| **Built-in content review** (POLL-09) | English only; the audit flagged tone for some prompts. | Review and localise, or leave. See also the draft trivia pack above. |

## Known weaknesses (not bugs, but worth knowing)

- **Bandwidth in Sketch Guess.** Every guesser receives the whole drawing on each push. The caps are now 400 strokes and 6000 points, so the worst-case push is larger than before (not measured). The proper fix is sending only new strokes, which is a protocol change.
- **One server instance only.** `RoomSnapshotSequencer` and `PresenceTracker` live in process memory. The code says so. Scaling out needs a shared backplane first.
- **Setup rules are written twice** (server and each game's setup form). Accent-duplicate bugs came from that three times. A shared rule or a server "validate setup" endpoint would remove the class of bug.
- **Buzzer fairness** goes to whichever request reaches the server first, so a slower connection loses.
- **Sketch Guess has no keyboard way to draw.** Guessers have a full keyboard path.
- **No spectator role.** Everyone in a room is a player.
- **Leftover strokes** stay in the database if a host ends a Sketch game mid-drawing, until the room is deleted.
- **Sketch cap counts a long drag as many strokes** (a new stroke every 500 ms). Raising the caps hid this rather than removing it.
- **The 8-player browser race run** was not repeated after the final fix; only the API-level race was.

## Not yet tested (the audits could not cover these)

- Real phones and tablets (everything was emulation), including iOS Safari keyboard and safe-area behaviour.
- A screen reader. Live-region text was inspected only.
- Brave on tablet and phone landscape. The 12-player case beyond Would You Rather. Refresh in every phase for all 19 games. Slow-3G runs for 14 of the 19 games.
- Dev-only: a React StrictMode console message (LEAD-03) from the realtime connection teardown; not seen in production.

## Process improvements worth doing

- **Put browser tests in the repo.** The audit's Playwright scripts live outside the repo, so the races and layout fixes are only protected by unit tests. Turn the End-versus-answer race, the 8-player simultaneous-action run and the 320 px layout check into a CI job. Needs a decision: adds Playwright as a dev dependency and a browser step in CI.
- **Load test.** Nothing has measured many concurrent rooms or the connection pool size. Do this before real use, and decide a backup and recovery plan for the database.
- **Audit logging.** There is no tamper-resistant record of who did what beyond game state. Probably fine for a party-game app; say so explicitly if so.

## How to run it

- Postgres: the container `randomroom-pg` on port 5433 (`docker start randomroom-pg`). `backend/RandomRoom.Api/.env` holds the connection string and signing key (not in git; copy from `.env.example`).
- API: `cd backend/RandomRoom.Api && dotnet run --no-launch-profile --urls http://localhost:5184`
- Frontend dev: `cd frontend && npm run dev` (proxies `/api` and `/hubs` to port 5184).
- Production build: `npm run build` in `frontend` writes into `backend/RandomRoom.Api/wwwroot` (git-ignored); the API then serves the app and sets the security headers.
- Backend tests: `dotnet test backend/RandomRoom.slnx` (needs the Postgres above). Frontend: `npx vitest run`, `npm run lint`, `npx tsc -b`.
- If an old API process is holding port 5184, you will see confusing "unknown game type" errors. Stop it first.
- `pkill -f RandomRoom.Api` can match your own shell; use `pkill -x RandomRoom.Api`.
- Testing leaves rooms titled "verify" in the local `randomroom` database. They are harmless.
