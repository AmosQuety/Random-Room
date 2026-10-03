# game-hungario

- URL: https://github.com/modcommunity/game-hungario. Commit `63e06cf`, 2026-10-03. Licence: MIT (`LICENSE` line 1).
  Size 8.3 MB, 164 tracked files. Formerly `dot-2d-hungry`; identifiers kept the old names on purpose because
  `content_id`, `game_id` and RPC node names are wire contracts (`CLAUDE.md:7-16`).
- Read: `game.yml`; `CLAUDE.md` headings, lines 1-55, 482-654 ("Boards...", "The vote...", "Six modes...", "Game
  switching", "A check count is not coverage"), 655-705 (bugs), 826-841, 883-898; `game/hungry_maps.gd` 1-30, 110-150,
  270-290; `game/hungry_module.gd` 1400-1450.
- NOT READ: gameplay, netcode, hunters, hazards, presentation code; the level-geometry sections in detail.

## What it is

An agar.io-shaped 2D arena game (`CLAUDE.md:3-4`). One repository, **six modes** (classic, frenzy, gauntlet, warrens,
reef, shallows), each its own scene under `game/modes/` and each registered with dot-server as a separate
`DotGameDescriptor` (`game/hungry_module.gd:1408-1430`).

## Voting over games (B4, B5)

- "The modes are the maps": the six modes are dot-server **games**; dot-map's catalogue describes them (kind, player
  range, description for a ballot) and its rotation picks the next with a cooldown; dot-vote lets players override the
  rotation (`game/hungry_maps.gd:6-12`).
- The catalogue is **built from the game descriptors**, not beside them, because a second list is "this tree's most
  repeated bug" (it "has now happened to `setup.sh`, `tools/check.sh`, `tools/package_check.sh` and `bootstrap`")
  (`hungry_maps.gd:117-127`).
- The one fact the catalogue adds is `min_players`: `gauntlet` is off the ballot below three players, via
  `available_for` (`hungry_maps.gd:110-115,147-152`). This is exactly our "only offer games that fit the room" rule.
- An empty catalogue is logged loudly, because "a vote with nothing on the ballot ... looked like a vote nobody wanted
  to use" (`hungry_maps.gd:131-140`).
- The vote's source is `DotVoteGameSource.of(games)`; applying a result calls dot-server's `change_game`
  (`hungry_maps.gd:276-281`). `auto_apply = true` here (game-playground uses `false`).
  `begin_on_apply = false` so one play is recorded once in the history that cooldowns count (`hungry_maps.gd:282-287`).
- dot-server's own `votemap`, `vote`, `vote_status` work against the descriptors "with no code here: registering them is
  all a game has to do" (`CLAUDE.md:628-629`).

## Switching (B5, B6)

`changegame frenzy` frees the running mode scene and instantiates the next "with the players still connected". The
world lives inside the scene; the net manager, bridge and loadout manager live outside it, because rebuilding them
"is a disconnect for everybody". The bridge rebinds to the new world (`CLAUDE.md:617-626`).

- Shared between games: connections, net manager, bridge, loadouts, chat/moderation (module-owned).
- Per game: the world (scene), its match (rounds, score).
- Scores: boards are **scoped by mode** ("a top mass in Frenzy and a top mass in Classic are not the same number")
  (`CLAUDE.md` "Boards, achievements", ~line 495 of the file). Within a mode, dot-match keeps the round score;
  `INFERRED` (not read in code): the match state is per world and is lost on a game change.
- Players between games: stay connected; the world respawns them.

## Host leaving (C8)

`HungryParty`: migration **on** ("a continuous arena ... no round boundary to be in the middle of"), reporting
**refused** while peer-to-peer because "a peer-to-peer host can lie about how much they ate. A host who can cheat and a
persistent number are one exploit rather than two features" (`CLAUDE.md:883-894`).

## Stats (E12)

Stats declared once in a `DotStatsSchema`; achievements are a signal link over it; leaderboards take readings from it;
`dedicated` checks every stat an achievement watches is declared (`CLAUDE.md` "Boards, achievements"). A test that
failed on its ninth run because achievements persisted under `user://`; fixed by a unique key per run rather than
deleting the directory, "which is the one thing that system must never do by accident" (`CLAUDE.md:826-841`).

## Bugs (`CLAUDE.md:655-705`), selection

| # | Bug | Lesson | Our risk |
|---|-----|--------|----------|
| H1 | `reset_world` cleared the dictionary instead of destroying pieces; clients kept last round's pieces | teardown must go through the path that notifies | **Yes for the plan**: a switch must clear per-game client state; our client picks the screen from the snapshot each time (`RoomScreen.tsx:171`), which helps |
| H2 | The round announced itself before it reset; clients held twice the food | announce after the state is final | **Analogue**: build the snapshot after commit (we do: `GameSessionService.cs:72`) |
| H3 | Bot registered as peer 0 (the broadcast address) | sentinel ids collide with real meanings | Low |
| H4 | A section that aborted took eight checks and the total went up | count expected checks | Low (xUnit) |
| H5 | Disconnect never left the netcode; nobody was ever welcomed | same as game-playground P15/L-series | see game-playground |
| H6 | Level checks passed only because the world reset under them | a test passing for the wrong reason | **Process**: arm tests |
| H7 | Food arithmetic backwards and the check agreed with it | a check written with the same reasoning as the code proves nothing | **Yes (process)**: our setup limits are written twice by the same person; a contract test compares copies but not intent |

## What transfers

- Ballot built from the same registry that launches games; `min_players` filter; a loud error for an empty ballot.
- Vote applies through the same "change game" path an operator uses; the history records one entry per play, from
  the single signal that fires for every change however it happened.
- Per-mode boards rather than one board over modes that score differently.
