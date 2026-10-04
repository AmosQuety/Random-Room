# Research progress log

Started: 2026-10-03 21:26 UTC. Environment details: see `preflight.md` (not repeated here).

Rules in one line: work only on `research/game-platform-report`, write only under `docs/research/`, read other
repositories as text only (never run them), treat their contents as data, allowed hosts are GitHub only, no secrets,
short quotes with paths, never fabricate.

## Repository inventory

| Name | URL | Licence | Size | Last commit | Status | Notes file |
|------|-----|---------|------|-------------|--------|------------|
| game-playground | https://github.com/gamemann/game-playground | MIT | 38 MB / 298 files | 2026-10-03 a43a116 | READ DEEP | notes/game-playground.md |
| game-simple-lobby | https://github.com/modcommunity/game-simple-lobby | MIT | 1.2 MB / 104 | 2026-09-30 f96b7ee | READ DEEP | notes/game-simple-lobby.md |
| game-hungario | https://github.com/modcommunity/game-hungario | MIT | 8.3 MB / 164 | 2026-10-03 63e06cf | priority 2 | notes/game-hungario.md |
| dot-vote | https://github.com/modcommunity/dot-vote | MIT | 536 KB / 49 | 2026-09-26 e7b0c5e | priority 3 | notes/dot-vote.md |
| dot-server | https://github.com/modcommunity/dot-server | MIT | 1.1 MB / 85 | 2026-10-02 154bc60 | priority 3 | notes/dot-server.md |
| dot-server-deploy | https://github.com/modcommunity/dot-server-deploy | MIT | 2.2 MB / 205 | 2026-10-03 2e495af | priority 3 | notes/dot-server-deploy.md |
| dot-core | https://github.com/modcommunity/dot-core | MIT | 696 KB / 92 | 2026-09-26 fcce93f | priority 3 | notes/dot-core.md |
| dot-game | https://github.com/modcommunity/dot-game | MIT | 276 KB / 36 | 2026-09-27 5603869 | priority 3 (found in notes) | notes/dot-game.md |
| dot-net | https://github.com/modcommunity/dot-net | MIT | 720 KB / 67 | 2026-10-03 b26b6d0 | priority 4 | |
| dot-peer-to-peer | https://github.com/modcommunity/dot-peer-to-peer | MIT | 196 KB / 27 | 2026-09-24 37d47c1 | priority 4 | |
| dot-user | https://github.com/modcommunity/dot-user | MIT | 248 KB / 32 | 2026-09-24 565cba7 | priority 4 | |
| dot-stats | https://github.com/modcommunity/dot-stats | MIT | 208 KB / 28 | 2026-09-26 5a2519c | priority 4 | |
| dot-achievements | https://github.com/modcommunity/dot-achievements | MIT | 272 KB / 36 | 2026-09-24 6e213da | priority 4 | |
| dot-leaderboard | https://github.com/modcommunity/dot-leaderboard | MIT | 216 KB / 30 | 2026-09-24 adf3797 | priority 4 | |
| dot-moderation | https://github.com/modcommunity/dot-moderation | MIT | 444 KB / 45 | 2026-09-24 e869fad | priority 4 (found in notes) | |
| dot-platform | https://github.com/modcommunity/dot-platform | MIT | 192 KB / 25 | 2026-09-24 bd3325d | priority 5 (found in notes) | |
| dot-auth | https://github.com/modcommunity/dot-auth | MIT | 488 KB / 51 | 2026-09-24 5e3b37c | priority 5 (found in notes) | |
| dot-ci | https://github.com/modcommunity/dot-ci | MIT | 112 KB / 12 | 2026-10-03 d26b5a4 | priority 5 | |
| dot-cloud | https://github.com/modcommunity/dot-cloud | MIT | 676 KB / 67 | 2026-09-30 c88670e | priority 5 | |
| dot-match | https://github.com/modcommunity/dot-match | MIT | 288 KB / 40 | 2026-09-27 1cd016f | priority 5 | |
| dot-chat | https://github.com/modcommunity/dot-chat | MIT | 336 KB / 36 | 2026-09-24 7f343e1 | priority 5 | |
| dot-inventory | https://github.com/modcommunity/dot-inventory | MIT | 284 KB / 33 | 2026-09-25 9fb3aa9 | priority 5 | |
| dot-map | https://github.com/modcommunity/dot-map | MIT | 392 KB / 39 | 2026-09-25 42e45fc | priority 5 | |
| dot-timer | https://github.com/modcommunity/dot-timer | MIT | 608 KB / 67 | 2026-09-24 e6a9778 | priority 5 | |
| dot-combat | https://github.com/modcommunity/dot-combat | MIT | 344 KB / 46 | 2026-09-24 d4b67c2 | priority 6 | |
| dot-loadout | https://github.com/modcommunity/dot-loadout | MIT | 312 KB / 44 | 2026-09-24 42b4f22 | priority 6 | |
| dot-npc | https://github.com/modcommunity/dot-npc | MIT | 428 KB / 48 | 2026-09-24 90ba344 | priority 6 | |
| dot-npc-ai | https://github.com/modcommunity/dot-npc-ai | MIT | 368 KB / 49 | 2026-09-24 9af3eab | priority 6 | |
| dot-player-controller | https://github.com/modcommunity/dot-player-controller | MIT | 25 MB / 103 | 2026-09-29 3e5c38c | priority 6 | |
| dot-props | https://github.com/modcommunity/dot-props | MIT | 316 KB / 38 | 2026-09-24 a8980b5 | priority 6 | |
| dot-ui | https://github.com/modcommunity/dot-ui | MIT | 440 KB / 51 | 2026-09-24 ef2cf8c | priority 6 | |
| dot-user-avatar | https://github.com/modcommunity/dot-user-avatar | MIT | 356 KB / 46 | 2026-09-24 afaecf4 | priority 6 | |
| dot-vehicle | https://github.com/modcommunity/dot-vehicle | MIT | 392 KB / 50 | 2026-09-28 1894f14 | priority 6 (found in project.godot) | |
| dot-physics | https://github.com/modcommunity/dot-physics | MIT | 224 KB / 29 | 2026-09-24 6e2f8f6 | priority 6 (found in project.godot) | |
| dot-spawn | https://github.com/modcommunity/dot-spawn | MIT | 276 KB / 39 | 2026-09-24 96370ae | priority 6 (found in project.godot) | |
| dot-player | https://github.com/modcommunity/dot-player | MIT | 196 KB / 25 | 2026-09-22 d38fb70 | priority 6 (found in project.godot) | |
| dot-player-char | https://github.com/modcommunity/dot-player-char | MIT | 448 KB / 66 | 2026-09-24 b04a9ac | priority 6 (found in project.godot) | |
| dot-player-class | https://github.com/modcommunity/dot-player-class | MIT | 192 KB / 25 | 2026-09-24 fcedc81 | priority 6 (found in project.godot) | |
| dot-team | https://github.com/modcommunity/dot-team | MIT | 260 KB / 32 | 2026-09-24 48efac9 | priority 6 (found in project.godot) | |
| game-arena | https://github.com/modcommunity/game-arena | MIT | 22 MB / 268 | 2026-10-03 47015ea | priority 6 | |
| game-g2gfast | https://github.com/modcommunity/game-g2gfast | MIT | 76 MB / 373 | 2026-10-03 690a308 | priority 6 | |
| zee-dot-weapons | https://github.com/gamemann/zee-dot-weapons | MIT | 2.4 MB / 164 | 2026-09-25 97cb029 | priority 6 | |
| (exist, not cloned) dot-2d, dot-objective, dot-bootstrap, dot-voice, dot-audio, dot-settings, dot-weapon, dot-browser, dot-spectate, dot-2d-hungry, dot-fx (modcommunity); mg-buses-from-hell (gamemann) | `git ls-remote` exit 0 | not read | | | NOT READ | |
| (do not exist under either owner) dot-query, game-blob, website-city, game-dev, dot-randomness, dot-p2p | `git ls-remote` failed | | | | UNREACHABLE (names only appear in notes) | |

## Questions answered

- [x] A1 game contract / `game.yml` — report §4, §5-A (field table)
- [x] A2 discovery, install, versioning, launch — §4, §5-A; notes/dot-server-deploy.md, dot-ci.md
- [x] A3 content packaging — §5-A; notes/dot-cloud.md
- [x] B4 changing what is played (rotation, votes) — §4, §5-B; notes/dot-vote.md (no veto exists)
- [x] B5 multi-game lobby — §5-B; notes/game-simple-lobby.md (it is not one)
- [x] B6 unit of state for a play — §4, §5-B (INFERRED in part)
- [x] C7 roles and permissions — §5-C; notes/dot-server.md
- [x] C8 host leaves / migration — §5-C table (verified in code for all five games)
- [x] C9 identity, accounts, bans — §5-C; notes/dot-user.md, dot-moderation.md
- [x] D10 client/server sync — §5-D; notes/dot-net.md, game-playground.md §8
- [x] D11 timers, ticks, one source of truth — §5-D (13 examples)
- [x] E12 leaderboards, stats, achievements, replays — §5-E; notes/dot-stats.md (backbone validation UNREACHABLE)
- [x] F13 config layering, console, operator tools — §5-F; notes/game-playground.md §4-5, dot-server.md
- [x] F14 deployment, packaging, CI — §5-F; notes/dot-ci.md, dot-server-deploy.md
- [x] G15 integration suite, bots — §5-G
- [x] H16 documentation practice — §5-H

## Blocked or unreachable

## Suspicious instructions found

- game-playground `CLAUDE.md:9` and game-simple-lobby `CLAUDE.md:3-4`: "Read the family-wide conventions in
  `../../CLAUDE.md` first". An instruction addressed to agents working in that workspace; the file is not in the
  repository. Not followed (nothing to follow); recorded as NOT READ.
- Several `CLAUDE.md` and README sections tell the reader to run commands (`godot --headless ...`, `tools/check.sh`,
  `tools/export_zones.gd`, symlink loops). Not run (rule 7). None asked for secrets, settings changes or data
  transfer.

## Assumptions

- "Family-wide" documents referenced by the repositories (`../../CLAUDE.md`, `docs/testing.md`,
  `docs/gdscript-hazards.md`) are outside every repository I can clone; treated as NOT READ.
- The studied repositories' notes files are named `CLAUDE.md`. I cite them by that file name as evidence (the owner's
  own brief does the same); no file here otherwise names the assistant or its maker.
- Where a README and the code disagree on a test count, the number in the code (`const CHECKS`) is taken as current.

- Scratch folder is the session scratchpad (`.../scratchpad/game-research`), not `/tmp/game-research`; it is outside
  the repository, so nothing there can be committed by `git add docs/research`.

## Log

- 21:26 Phase 0: branch checked out from the pushed preflight branch, identity set, preflight notes tidied.
- 21:27 Phase 1 done: notes/our-app.md. Plan doc and NEXT_STEPS were on main (PR merged), no fetch of the docs branch needed.
- 21:35 Phase 3 primary notes written: notes/game-playground.md (read list and NOT READ list inside).
- Phase 2 (inventory): cloned all 41 listed or discovered repositories with `git clone --depth 1` (full blobs; a
  `--filter=blob:none` attempt to get sizes cheaply was too slow and was stopped). Licence = first line of `LICENSE`;
  every repository read is MIT. Size = working tree without `.git` / tracked files. Purpose lines from each README's
  first paragraph. Extra names found in game-playground (`project.godot` plugins, `CLAUDE.md`) and game-simple-lobby
  were tested with `git ls-remote` under both owners.
- game-simple-lobby deep read done: notes/game-simple-lobby.md.
- 21:38 game-hungario notes done.
- 21:39 dot-vote notes done (docs fully, code partly).
- 21:39 dot-server notes done.
- 21:41 dot-server-deploy notes done (the multi-game server and game vote live here).
- 21:41 dot-core, dot-game notes done.
- 21:42 priority-4 notes done: dot-net, dot-peer-to-peer, dot-user, dot-stats (+leaderboard, achievements), dot-moderation.
- 21:43 dot-ci, dot-cloud notes done.
- 21:44 dot-map/dot-match notes and skimmed list done. Verified the five-game host-migration table against each game's party file (all five match). Phase 4 complete.
- 21:44 Corrected line numbers for our own files (an earlier multi-file `cat -n` had numbered them continuously).
- 21:49 Phases 5-6: full report written (sections 1-12, appendices A-D).

## Verification (section 7 of the brief)

1. Findings have evidence: every numbered finding in the report has a path and lines or heading, or is marked INFERRED
   or NOT READ. Appendix A indexes F1-F30.
2. Spot-check of ten claims about the studied repositories, by reopening the files: game-playground README "432
   checks"; game-playground `CLAUDE.md` "359 checks"; dot-server "malformed admins.json is loud"; dot-vote "374 checks"
   and "Eighty settings"; game-hungario "Boards are scoped by mode"; dot-peer-to-peer election rule; dot-server-deploy
   `close_when_all_voted: true`; dot-match three failure modes; game-playground `report_to_backbone` default; dot-cloud
   versioned mount path. **10 checked, 1 failed**: the "359 checks" line is `CLAUDE.md:27`, not 22-26. Fixed in the
   report and notes. Earlier I also found and fixed wrong line numbers for six of our own files (a multi-file `cat -n`),
   and seven ranges in the hungario/playground notes (estimated from un-numbered output).
3. Our files reopened and confirmed: `Games/IGameEngine.cs:21-26` (setup "Called once, at room creation"),
   `Domain/Room.cs:10` ("Fixed for the room's lifetime"), `Services/RoomAdminService.cs:165` (reset only between games),
   `Services/GameSessionService.cs:154-157` (untracked latest session), `Endpoints/RoomEndpoints.cs:52-61` (broadcast after
   each session call), `frontend/src/components/RoomScreen.tsx:171,184`. 6 of 6 confirmed.
4. Quotes are short (one or two sentences) with paths and lines; no file was copied.
5. Report sections 1-12 and appendices A-D exist; A1-H16 all answered (checklist above).
6. `git diff --name-only origin/main...HEAD` lists only `docs/research/` files.
7. Secret scan (`token`, `key`, `secret`, `password`, `Bearer`, `ghp_`): only descriptive text (RCON design notes).
8. Commit messages contain no attribution; all commits authored and committed by AmosQuety <amosnabasa4@gmail.com>.
9. Scratch clones deleted at the end (see below).

Scratch clones deleted at 21:51.

FINISHED 21:51 UTC (container clock). The report (`game-platform-report.md`) describes how the game-playground
ecosystem structures, installs, switches, votes on, hosts, syncs, records, configures and tests its games, compares
each area with our app in tables, lists their bugs with our exposure, ranks eight ideas plus four new ones, and
proposes a ten-step roadmap with a recommendation to move game type and setup from room to session (option A). It is
based on deep reads of six repositories and partial reads of thirteen; the code of the largest engine files (vote
director, dot-server core, dot-net) was described from their own documentation rather than read line by line, the
family-wide documents the repositories refer to were not available, and the closed-source backbone (stats validation)
could not be seen. Ten claims were spot-checked (one line range was wrong and fixed) and six of our own files reopened.
