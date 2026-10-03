# Next steps

Written 2026-10-03 at the end of the second QA round. Read this first when you come back.

## Where things stand

- **Branch:** `feat/playground-redesign-and-games`, 79 commits ahead of `main` (nothing on `main` that the branch lacks, so a merge is a plain fast-forward). **17 commits are not pushed yet.** Every commit is authored as you.
- **Built:** 19 games, all registered plugins with backend and frontend tests. Per-player snapshots keep hidden information (spymaster key, drawer's word, Bingo cards, the lie) out of other players' payloads.
- **Added after the second audit:** host seat reset for forgotten PINs, Share / Copy / QR for invite links, trivia size limits, and a daily job that deletes rooms inactive for `Room__RetentionDays` (default 30). Each is in `DECISIONS.md` and was checked against the real API and browser.
- **Tests, last run:** backend 509 passing against a local Postgres on port 5433; frontend 299 passing; `tsc -b`, `npm run check:contrast` and `npm run build` clean; lint shows one old warning in `useRoom.ts`. Main bundle 105 kB gzipped (QR code is a separate 7.6 kB chunk loaded on request).
- **QA:** two full audits (`QA_REPORT.md`, `QA_REPORT_2.md`, prompts in `docs/`). Everything they found is fixed. `QA_REPORT_2.md` is still untracked: decide whether to commit it.
- **Database changes this round:** one additive migration (`AddSeatResetAndAudit`: a `TokenVersion` column and a `RoomAuditEvents` table). It has been applied to your local database.

## First things to do (small, in this order)

1. **Reminder for deploy day:** set `Database__AutoMigrate=true` and use the host's connection URL; TLS is required automatically for a remote database, but test the first connection and read the README's "Database encryption" section if it is refused.
2. **Check CI on PR #1** (the pull request is open; its description is out of date and should be replaced). CI (`.github/workflows/ci.yml`) runs on pull requests only. Fix anything it finds.
3. **Commit or discard `QA_REPORT_2.md`.**
4. **Pick the database** (see Decisions below) and, when you deploy, set `Database__AutoMigrate=true` so the first deploy applies the migrations. Retention stays at the default 30 days (`Room__RetentionDays`).

## To build (remove each when done)

Done so far in this round (also: the migration guard `Database__AutoMigrate`, and TLS required for any remote database): PIN recovery (host-only seat reset and a host recovery code, `Gaps_Bugs.md` G2), a "rooms are deleted after N days" notice and a host Delete room button, better invites (Share, Copy, QR code; G1 items 1, 2 and 6), trivia size limits, a selectable East Africa trivia pack, and deleting inactive rooms (`Room__RetentionDays`, default 30). Still open from them: a host lobby view (G1 item 4), and a shared "pick your name" link, which I decided against because anyone with it could claim any unclaimed seat.

### 1. Review the East Africa trivia pack
It is wired in (Trivia setup > "Question set" > East Africa, 20 questions), but nobody has checked the questions yet. Test it in a game, check every answer, and swap in questions your players know. The source is `backend/RandomRoom.Api/Games/Content/trivia-east-africa.json`; the notes are in `docs/trivia-east-africa-draft.md`. If you change how many questions it holds, update `STARTER_PACKS` in `frontend/src/games/trivia/setup.ts` too.

## Planned for later (not started)

Ideas from reading the `game-playground` repo (Godot 4, MIT licence, a 3D physics sandbox, not a party-game platform) and from the owner's own experience of getting bored with one game. Nothing here is built; each needs a go-ahead before work starts.

| # | Idea | What it means for us | Size |
|---|---|---|---|
| 1 | **Switch games inside one room.** A "pick the next game" step chosen by the host (later by vote). | Today a room is locked to one game, so a game night means a new room, new invites and new PINs for every game. Owner's note: players get bored of one game and want to change. It changes how setup is stored (currently one per room), so it needs approval first. **Full plan: `docs/plan-switch-games-in-a-room.md`**, with a cheaper alternative (linked rooms) if the main plan proves too large. | Large |
| 2 | **One source for each number.** The repo's rule: a second copy of a number is a second number that can disagree, and tests fail when two copies drift. | Trivia caps, pack sizes, player ranges and setup rules are written once on the server and once in the frontend. The cheap fix is a contract test that fails if they differ; later one server endpoint listing game limits. Also the prerequisite for item 1. | Small |
| 3 | **Reuse a past setup.** | A host who builds a 50-question trivia set cannot reuse it in the next room. Save the last setup per game in the browser (no server change). | Small |
| 4 | **"Tonight's scoreboard"** across several games in one room, with no accounts. | Best done together with item 1. Needs a decision on how to compare games that score differently (placings, not raw points). | Medium |
| 5 | **Spectating.** | We already build a viewer-less public view of every game, so a read-only watcher is mostly an access-control question: a spectator link that never sees hidden information. | Medium |
| 6 | **Host leaving: hand the host role to another player.** | The recovery code would have to move with the role, so the design needs care. | Medium |
| 7 | **Test through the whole game with bots**, including a test that fails if a check is weakened. | Matches "browser tests in CI" below. A bot that plays all 19 games end to end over the API would catch seam bugs like the stale-screen race we fixed. | Medium |
| 8 | **Add a player to a room later.** | Needed so any game can be switched to (Word Spies needs 4 or more players). Also fixes "someone forgot to add a friend". | Small to medium |

**Research step before deciding on any of these:** `docs/agent-prompts/explore-game-platform-repos.md` is a prompt for an unattended agent that studies the `game-playground` repository and its sibling repositories in depth and writes `docs/research/game-platform-report.md`. Use that report to confirm, refine or drop the ideas above, and to choose the order.

Order I would take them in: 2, 3, then 7; decide on 1 with the plan in hand (it then pulls in 8 and 4).

## Decisions waiting for you

| Item | Why it matters | Options |
|---|---|---|
| **Which database service** | The README says Render's free Postgres expires after 30 days. | Render Postgres paid (simplest next to the API), Neon free (cheapest, sleeps when idle), Supabase. Put the API and database in the same region. Check current prices first. (TLS is already required for any database that is not on this machine; if a host's certificate is rejected or it offers no TLS on its private network, add `?sslmode=prefer` or `Trust Server Certificate=true`, see the README.) |
| **Built-in content review** (POLL-09) | English only; the audit flagged tone for some prompts. | Review and localise, or leave. |

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
