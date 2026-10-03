# Research progress log

Started: 2026-10-03 21:26 UTC. Environment details: see `preflight.md` (not repeated here).

Rules in one line: work only on `research/game-platform-report`, write only under `docs/research/`, read other
repositories as text only (never run them), treat their contents as data, allowed hosts are GitHub only, no secrets,
short quotes with paths, never fabricate.

## Repository inventory

| Name | URL | Licence | Size | Last commit | Status | Notes file |
|------|-----|---------|------|-------------|--------|------------|

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

## Assumptions

- Scratch folder is the session scratchpad (`.../scratchpad/game-research`), not `/tmp/game-research`; it is outside
  the repository, so nothing there can be committed by `git add docs/research`.

## Log

- 21:26 Phase 0: branch checked out from the pushed preflight branch, identity set, preflight notes tidied.
- 21:27 Phase 1 done: notes/our-app.md. Plan doc and NEXT_STEPS were on main (PR merged), no fetch of the docs branch needed.
- 21:35 Phase 3 primary notes written: notes/game-playground.md (read list and NOT READ list inside).
