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

- [ ] A1 game contract / `game.yml`
- [ ] A2 discovery, install, versioning, launch
- [ ] A3 content packaging
- [ ] B4 changing what is played (rotation, votes)
- [ ] B5 multi-game lobby
- [ ] B6 unit of state for a play
- [ ] C7 roles and permissions
- [ ] C8 host leaves / migration
- [ ] C9 identity, accounts, bans
- [ ] D10 client/server sync
- [ ] D11 timers, ticks, one source of truth
- [ ] E12 leaderboards, stats, achievements, replays
- [ ] F13 config layering, console, operator tools
- [ ] F14 deployment, packaging, CI
- [ ] G15 integration suite, bots
- [ ] H16 documentation practice

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
