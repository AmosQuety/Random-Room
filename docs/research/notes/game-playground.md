# game-playground (primary repository)

- URL: https://github.com/gamemann/game-playground (same commit as `modcommunity/game-playground`, per preflight)
- Commit read: `a43a116`, 2026-10-03. Licence: MIT (`LICENSE` line 1-3, "Copyright (c) 2026 game-playground contributors").
- Size: 38 MB clone, 298 files; ~42,700 lines of GDScript/Markdown/YAML.
- Read: `README.md` (all), `game.yml` (all), `CLAUDE.md` (192 KB, 1,788 lines; headings list, then sections listed
  below), `.github/workflows/*` (all), `project.godot`, `game/playground_config.gd` (all), `game/playground_party.gd`
  (all), `game/playground_vote.gd` (1-200 of 329), `game/playground_module.gd` (1-380 of 2,127, plus a grep of every
  `add_command`), `game/net/playground_events.gd` (enums), `game/playground_platform.gd` (1-60),
  `examples/*.gd` (headers, runners, section lists, the permissions test).
- NOT READ: the 3D gameplay code (`playground.gd` body, maps, NPCs, vehicles, weapons, client, HUD),
  `playground_services.gd` body, `playground_net_bridge.gd` body, `playground_inventory_net.gd` body (described from
  `CLAUDE.md` "The bag over the wire" only), `CLAUDE.md` sections about maps, routes and movement (lines ~247-757,
  1007-1135, 1168-1180, 1217-1375, 1419-1487, 1559-1686) except where quoted below.
- Note: the file's line 9 says "Read the family-wide conventions in `../../CLAUDE.md` first". That parent file (and
  `docs/testing.md`, `docs/gdscript-hazards.md` it cites) lives outside this repository in a workspace I cannot see.
  NOT READ / UNREACHABLE.

## 1. What it is

A Godot 4 first-person physics sandbox (props, physics gun, NPCs, weapons, vehicles) with surf/bunny-hop timed courses
(`README.md` intro and "The maps"). It is the integration point where ~20 `dot-*` addons run together
(`CLAUDE.md:13-31`; `project.godot` `[editor_plugins]` enables 15 addons). The README says it is "partially tested",
"very little of this has been in front of real players yet" (`README.md` "From Maintainer & WARNING"). The maintainer
states the project and every addon were built with an AI coding assistant (same section).

**It is one game, not a platform of several games.** It rotates **maps** inside one game (`game/playground_vote.gd:13-17`:
"this one votes over **maps** ... game-hungario votes over games"). This confirms the statement at the end of our
`docs/plan-switch-games-in-a-room.md`.

## 2. Layout (top level)

| Path | Purpose (evidence) |
|------|--------------------|
| `game.yml` | the descriptor a server reads when it installs this game's pack |
| `game/` | the game: simulation (`playground.gd`), operator config, server module, client, net wire (`game/net/`) (`CLAUDE.md:50-92`) |
| `maps/` | three built-in maps built in code, plus generated `*.zones.json` |
| `examples/` | the five headless suites (`headless_playground`, `headless_net`, `dedicated`, `headless_presentation`, `headless_stack`) |
| `tools/` | zone exporter, screenshot and audio probes (shell + GDScript) |
| `scenes/`, `assets/`, `images/` | scenes, art, preview GIF |
| `.github/workflows/` | `ci.yml`, `release.yml`, both calling reusable workflows in `modcommunity/dot-ci` |
| `addons/` | **not in the repo**: addons are symlinked from sibling checkouts for development, copied in for a build (`README.md` "Setting up") |

## 3. The game contract: `game.yml` (A1)

`game.yml` (34 lines) fields and where each is read (readers are in other repositories; see `dot-server-deploy`,
`dot-server`, `dot-vote` notes):

| Field | Value here | Meaning (from `game.yml` comments lines 1-16) |
|-------|------------|----------|
| `content_id` | `gamemann/game-playground` | address of the pack; **stamped by the installer**, placeholder here |
| `version` | `0.0.0` | stamped by the installer from the pack it verified (e.g. `1.2.0+r2`) |
| `kind` | `pack` | stamped |
| `name` | `Playground` | display name |
| `max_players` | 32 | operator-editable after install |
| `scene` | `scenes/pg_server.tscn` | server scene, relative to the pack mount |
| `client_scene` | `game/playground.tscn` | client scene |
| `module` | `game/playground_module.gd` | the `DotModule` a dedicated server loads |
| `cvars` | `pg_map_seconds: "0"` | console variables applied on install |
| `metadata.map_vote` | `trigger: rtv_only`, `duration_sec: 0` | read by `PlaygroundVote` through `DotVoteGameSource.running_game_metadata("map_vote")` (`game/playground_vote.gd:44-48,168-170`) |

The descriptor travels inside the pack; `./server install-games` with `TMC_GAMES=<owner>/<repo>` fetches the pack,
verifies it and writes it to `content/<repo>/game.yml` where an operator edits cvars and player counts (`game.yml:3-7`).
**A second copy of the same descriptor** lives in `dot-server-deploy/content/playground/game.yml`, and
`dot-server-deploy`'s `examples/install_descriptor` suite fails when `scene`, `client_scene`, `module` disagree
(`game.yml:13-15`). That is a contract test between two copies of one description.

The module (`game/playground_module.gd`) is the server-side entry point: `extends DotModule`, `_module_name/
_version/_description/_author` (lines 111-124), `_module_load` refuses to load if no `Playground` service is
registered ("A module that loaded and did nothing would leave a server that accepts players into a game that does not
exist", lines 127-138), then builds netcode, extras (services, arena, waves, mod tools, vote, progress), commands,
cvars, hooks. Shutdown: a "module unloads cleanly" test exists (`examples/dedicated.gd:1956`). It is the only file
that names dot-server (lines 19-27).

## 4. Console commands and permission classes (C7, F13)

Registered with `add_command(name, fn, help, permission)`; `.with_chat()` makes a command typable from chat as `!cmd`
(`game/playground_module.gd:149-319`, explanation 205-218).

| Permission | Commands |
|------------|----------|
| none (`""`, with chat) | `pg_timer pg_restart pg_style pg_track pg_top pg_cp pg_tp pg_cp_clear pg_nextmap pg_rtv pg_prop pg_undo pg_status` (+ `pg_spec`, `pg_achievements` with non-admin help, lines 282-313) |
| `CHANGEMAP` | `pg_zone pg_zone_mark pg_zone_spawn pg_zone_list pg_zone_undo pg_zone_save pg_map pg_extend pg_arena pg_waves pg_shop pg_credits pg_give pg_vote` |
| `GENERIC` | `pg_props_clear pg_services pg_inv` |
| `MUTE` | `pg_gag pg_mute` |

Reason given for `CHANGEMAP` on zones: "drawing a start line is editing the map's rules, and somebody who can do it can
invalidate every record on it" (`CLAUDE.md:989-990`; code comment `playground_module.gd:177`). The flags are checked by
dot-server's console, not by the game; the game's test asserts the flag on the registered command
(`examples/dedicated.gd:741-766`). Live moderator tools (noclip, freeze, god, slay, slap, `blind`, `beacon`) come from
dot-moderation via `PlaygroundModTools` (`CLAUDE.md:36-48`). Rule: "an admin's help can never make a time" — admin
tools taint the run (`CLAUDE.md:40`).

Operator tools and the need each meets (from `CLAUDE.md` "The server module" table, lines 974-987, and README):
`pg_status` everything at once; `pg_give`/`pg_inv` put into / read a bag; `blind <player>` blacks out one player's
screen (owner-only replicated flag); `beacon <player>` marks a player for everyone; `pg_gag`/`pg_mute`; `pg_vote`
open/next/status; `pg_arena`, `pg_waves`, `pg_shop` turn the server into a different game mode; zone painting for maps
whose authors never drew zones.

## 5. Config layering and invalid config (F13)

`game/playground_config.gd` extends `DotConfig`: exported defaults < JSON file < `PLAYGROUND_*` environment < `--pg-*`
command line (lines 3-5, 103-108). `validate()` returns `DotResult.fail` for an empty map or an out-of-range
`rtv_fraction` (111-124). Tick rate is deliberately **not** a field: it lives once in `server.cfg` as `sv_tickrate`
(7-12). Vote config layers: `vote_rules()` defaults < `game.yml metadata: map_vote:` < `user://cfg/playground_vote.json`
< `DOT_VOTE_*` < `--vote-*`; a layered result that does not validate "is refused whole and the defaults stand", logged
not fatal, "because a server that would not start over its vote file is one nobody can fix from a chat window"
(`game/playground_vote.gd:21-44,164-177`). Settings the game cannot drive (round-end trigger, score limit) are dropped
with an error log (`179-186`). `report_to_backbone` is off by default because publishing sends names off the server
(`playground_config.gd:67-72`).

## 6. Map vote (B4)

`PlaygroundVote` wraps dot-vote's `DotVoteDirector` with `DotVoteMapSource` (`game/playground_vote.gd:188-194`). The
defaults (`vote_rules()`, 100-152): time-limit trigger, 1800 s map, ballot 120 s before the end, 30 s ballot, 5 options,
extend on the ballot (900 s, at most 4), current map not on the ballot, plurality, tie-break by ballot order,
rock-the-vote at 60% after 180 s with at least 2 players, one nomination per player with seconding, ballot filled by
most-nominated, cooldown 2 plays, apply immediately. Three of these settings are set explicitly because dot-vote had
bugs in them (`extend_needs_majority`, `nomination_seconding`, `begin_on_apply`) (`CLAUDE.md:925-928`).

## 7. Host leaving (C8)

`game/playground_party.gd` (126 lines): a peer-to-peer "party" via dot-peer-to-peer with `max_peers = 6`,
`trust = HOST_AUTHORITATIVE`, `migrate_host = false` (55-65). The table of five games (14-20) is reproduced in the
report. `reporting_allowed()` answers "no" while a party session is live, so nothing from a friend-hosted session is
filed to real boards, "Asked in one place" (111-117).

## 8. Networking (D10)

- Wire: `PlaygroundEvents.Kind` (server -> client): HELLO, JOIN, LEAVE, MAP, PROP, PROP_GONE, HELD, WEAPON, TIMER,
  FINISH, NOTICE, SEAT, CHAT, COMBAT, MATCH, PROGRESS, VOTE, CLOCK, INVENTORY; `Ask` (client -> server): READY,
  SPAWN_PROP, GIVE_WEAPON, SELECT_TOOL, UNDO, CLEAR_MINE, RESTART, CHECKPOINT, RTV, STYLE, USE_VEHICLE, SAY, VOTE,
  LOADOUT, INVENTORY (`game/net/playground_events.gd:17-97`). Encoders and decoders in pairs; the suite round-trips
  every one (lines 3-9).
- Snapshots and input prediction come from dot-net (NOT READ in this repo's bridge).
- Inventory sub-protocol (`CLAUDE.md` "The bag over the wire", ~1488-1513): client sends **ops only** (MOVE, SPLIT,
  MERGE, DROP), each with a sequence number; server acks cumulatively; a refusal rewinds everything in flight and
  replays survivors; a lost op is detected by a later ack or by a 3 s overdue timer, which triggers a whole-bag RESYNC;
  server-originated changes are sent as the **whole document**, which "converges"; only ever to the owner; keyed by
  the person's pseudonymous id, not the connection; every HELLO resets client sequence; 30 ops/s rate limit with the
  server's own actor exempt.
- READY gate: broadcasts go only to peers that have said READY; a joiner learns everyone from its admission
  (`CLAUDE.md:1687-1693`). Bugs this found are in the table below.
- Hidden information: per-player data (bag, blind flag) is sent owner-only; "`PlaygroundInventoryNet` is handed a way
  to reach one peer and no way to reach everybody, so a later edit cannot broadcast a bag by picking the wrong helper"
  (`CLAUDE.md` bag section).

## 9. One source of truth (D11)

Examples in this repository:
1. Tick rate: `server.cfg sv_tickrate` -> engine -> `Playground.tick_rate` -> timer -> record; no `pg_tickrate` cvar
   (`CLAUDE.md:758-778`, `991-993`; `playground_config.gd:7-12`). Test: `examples/dedicated.gd:464`
   `_test_tickrate_reaches_the_timer`, configured at 100 "precisely because the project's own default is 128: a test
   using the same number at both ends would pass with the chain disconnected".
2. Loadout catalogue built from `PlaygroundWeapons`, "A second list is the bug this tree has now shipped four times"
   (`CLAUDE.md:845-849`).
3. Chat channels offered by the UI come from the server's routing definitions (`CLAUDE.md` "The chat box").
4. Server-browser row counts read the spawner's and module's own tallies (`CLAUDE.md` "The server browser").
5. Built-in maps build geometry and zones from the same constants; delivered zone files are generated and checked
   (`CLAUDE.md:1143-1160`, test `_test_zone_file_matches_the_map`).
6. Every stat an achievement watches must be one the game declares; checked in `dedicated` (`CLAUDE.md:922-923`).
7. Stats recorded from signals the game already fires: "a second count of anything is a second number that can
   disagree" (`CLAUDE.md:917-920`).
8. `game.yml` vs `dot-server-deploy/content/playground/game.yml`, checked by `install_descriptor` (`game.yml:13-15`).
9. dot-server-deploy's selftest fails for a game that stops its map clock without stopping the vote's
   (`CLAUDE.md:946`).
10. Sound cue ids named once in `playground_vote.gd:50-55`.

## 10. Persistence and stats (E12)

Records (times, replays) stored under `records_directory` (`playground_config.gd:60-65`). dot-stats and
dot-achievements keyed by dot-user's **scoped pseudonymous id**; without identity, by session id
("lasts exactly as long as the session, which is honest") (`CLAUDE.md:952-966`; `game/playground_platform.gd` header).
Name changes off here because of the leaderboard (`CLAUDE.md:965-966`). Nothing is filed during a P2P party.

## 11. Testing (G15)

Five suites, run as headless Godot scenes. Declared totals in code: `headless_playground` CHECKS 563, SECTIONS 31
(`examples/headless_playground.gd:52-58`); `headless_net` 294/31 (`headless_net.gd:58-64`); `dedicated` 224/25
(`dedicated.gd:48-49`). **Doc drift:** README says 432, 271/28, 217/24, 99, 40 (`README.md` "Validating");
`CLAUDE.md:22-26` says 359 for headless_playground; `CLAUDE.md:1181-1196` says 563/31, 294/31, 224/25, 107, 40.

- A suite fails (exit 1) if the number of sections entered != completed != SECTIONS, or passed+failed != CHECKS,
  because "a runtime error inside a section aborts that function" and would otherwise leave "0 failed"
  (`headless_playground.gd:128-152`; `dedicated.gd:36-47`).
- **Bots**: the player's input is a scripted `DotFpsCommand` instead of a device; the bot drives down a surf map,
  finishes, files and ranks a run (`headless_playground.gd:20-46`). Lesson: "A bot keeps the last command it was given"
  (`CLAUDE.md:240-246`).
- **"Armed"**: each guard is verified by deliberately breaking the code and confirming the check fires ("armed both
  ways", "armed by dropping `to_owner_only()`: two fired"), then restoring it. Recorded in prose in `CLAUDE.md`
  (e.g. lines 46, 808, 1694-1695); there is no automated mutation tool in this repo.
- `dedicated` boots a real `DotServer` without sockets and types commands at its console (`dedicated.gd:19-34`).
- `headless_net` runs client and server halves in one process over a loopback link, including packet loss, reconnect
  and privacy against a second peer (section list `headless_net.gd:214-3494`).
- Screenshots via `xvfb-run` found bugs no assertion could (`CLAUDE.md:213-218,1197-1203`).
- "Run the suite twice": a test depended on how many times it had been run (`CLAUDE.md:728-733`). Order sensitivity
  measured in a table rather than called flaky (`CLAUDE.md:735-753`).

## 12. CI and release (F14)

`ci.yml`: on push to any branch (ignoring Markdown and screenshots), pull request and manual; calls
`modcommunity/dot-ci/.github/workflows/validate.yml@v1` with a 900 s suite timeout and a `DEPS_TOKEN` secret.
Comment: "branches: ['**'] rather than [main] on purpose" because repositories sit on task branches for days.
`release.yml`: on `v*` tags, validate then `dot-ci release.yml@v1.2.1` with `pack: true`; it "publishes nothing to the
content origin and signs nothing. A mounted resource pack can contain scripts and can never be unmounted, so the
private signing key lives in one deployment and not in sixty release workflows."

## 13. Bugs and lessons (from CLAUDE.md)

| # | Bug | Root cause | Lesson | Do we share the risk? |
|---|-----|-----------|--------|-----------------------|
| P1 | `effect_requested` signal emitted by nothing; fall-off zones did nothing (`CLAUDE.md:176-191`) | consumer existed in two games, producer in none | "an encoder and a decoder that have never met" — test that each consumer has a producer | **Yes, partly**: a SignalR event the client listens to that the server never sends would fail silently. We have 3 events (`roomChanged`, `seatReset`, `roomDeleted`; `Services/RoomNotifier.cs:15-17`, `frontend/src/lib/useRoom.ts:101-103`). Small surface. |
| P2 | Prop mass not applied (`CLAUDE.md:193-194`) | definition field read by nothing | value computed and consumed by nothing | Unknown; analogous to setup fields the engine ignores |
| P3 | UI controls 0x0 size (`CLAUDE.md:196-205`) | wrong API; tests checked properties, not size | measure outcomes, not configuration | Low; our layout is CSS, checked by the QA audits |
| P4 | Black screen: awaited a signal already emitted (`CLAUDE.md:209-218`) | "A signal is not a state" | check state, then wait for the event | **Already handled**: `useRoom` does a full refetch on connect and reconnect (`useRoom.ts:105-111`) rather than waiting for a push |
| P5 | Statistics discarded at run end (`CLAUDE.md:149-155`) | guard "only while active" refused the one call that matters | a refusal that is legitimate looks like a bug that is silent | **Possible**: our completion runs `IsSessionCompleteAsync` after an action; a final score written after status flips to Completed would be refused. Unknown without a test |
| P6 | Prespeed clamp dead: asked the wrong query (`CLAUDE.md:166-171`) | "a guard that never fires looks exactly like a guard that was never needed" | **arm** guards: break them and see a test fail | **Yes** (process): our tests assert refusals exist, but nothing proves a check would fail if a guard were removed |
| P7 | Weapons free on shop servers; client armed itself without asking (`CLAUDE.md:1376-1400`) | client-side decision with no server request; decoder was `pass` | "a client that arms itself looks exactly like a client that was given one" | No: every action of ours goes through `POST /api/room/action`, the server decides |
| P8 | Two unknown players "zero metres apart" (`CLAUDE.md:1403-1404`) | sentinel value shared by two unknown ids | a guard correct for one argument can be wrong for two | Low; our actor checks use `RoomPlayers` existence (`GameSessionService.cs:55-57`) |
| P9 | One player, two entity ids depending on modes (`CLAUDE.md:1406-1410`) | id minted by a layer that only exists in one mode | identity must be allocated once, by the owner, before any layer | **Relevant to the plan**: if setup/scores move to session, a player must keep one identity across sessions; we key by `RoomPlayer.Name` |
| P10 | Damage with no tick refused forever (`CLAUDE.md:855-861`) | default value 0 meant "before spawn protection" | defaults that mean something are dangerous | Low |
| P11 | `has_reacted()` false forever (`CLAUDE.md:900-913`) | right arithmetic, wrong field | test with the real producer, not a hand-built input | Unknown |
| P12 | Vote ran at double speed beside a second clock and a second rock-the-vote (`CLAUDE.md:930-935`) | two drivers advanced the same clock; two commands with one name | one clock, one driver; assert the director does not self-process | **Yes for the plan**: a next-game vote must have one deadline owner. Ours would be `ServerTimer` with a lazy `tick`, so one owner already |
| P13 | Clients' HUD showed a clock the server did not have (`CLAUDE.md:936`) | client ran its own clock from map load | send the authoritative clock; draw none when there is none | **No**: we send `TimerView(DeadlineAt, ServerNow)` (`ServerTimer.cs:4,19`) |
| P14 | `pg_status` reported the wrong clock (`CLAUDE.md:937`) | status read a stale source | status must read the owner | Low |
| P15 | Broadcast to peers that had not finished loading; welcome never sent; disconnected peers kept (`CLAUDE.md:1689-1695`) | "ready" was not modelled; sends bypassed the gate | send only to ready peers; one gate | **Partly**: we push only to online players (`RoomNotifier.cs:57-58`) and the client refetches after connect, so a push before the client is ready is harmless |
| P16 | Disconnect handler never ran (arity) (`examples/dedicated.gd:770-781`) | signature mismatch; nothing had ever disconnected in tests | test a real disconnect | **Partly**: `PresenceTracker.Disconnected` is unit-tested directly (`backend/RandomRoom.Tests/RoomServiceTests.cs:194-203`), but no test drives `RoomHub.OnDisconnectedAsync` (grep finds no `RoomHub` in the tests), so the hub wiring itself is untested |
| P17 | Suite result depended on how many times it ran (`CLAUDE.md:728-733`) | state persisted under `user://` between runs | run the suite twice | No: each backend test uses a throwaway database (`README.md` "Test") |
| P18 | Order-sensitive suite called "flaky" (`CLAUDE.md:735-756`) | shared physics space | measure placement, do not re-run to believe | Low (no shared physics); our analogue is shared static state such as `RoomSnapshotSequencer.Shared` |
| P19 | A section aborted by a runtime error left "0 failed" (`dedicated.gd:36-47`) | test framework counts failures, not completions | count checks and sections and compare to an expected total | Low: xUnit reports each test separately |
| P20 | Test runs wrote 352 test punishments into the real store (`playground_module.gd:55-63`) | default path shared by tests and production | inject storage paths | No: tests use throwaway databases |
| P21 | 32 commands registered, none reachable from chat (`playground_module.gd:205-212`) | default `chat_allowed=false` | a default can silently disable a feature | Low |
| P22 | Presentation hooks called only by tests, client silent (`CLAUDE.md` "The presentation layer") | test drove the hooks directly instead of the producer | drive the real producer | **Yes (process)**: our engine tests call engines directly; nothing drives the full REST + SignalR path end to end in CI |

## 14. What transfers and what does not

Transfers: the descriptor-plus-contract-test idea; counted, "armed" suites; one-source-of-truth discipline; whole-state
resync on doubt; owner-only sends by construction; refusal of invalid config as a whole with defaults standing; the
"migrate or not" reasoning per game; `reporting_allowed()` asked in one place.

Does not transfer: tick loops, prediction/rollback of movement, physics, zones, map geometry, P2P transport, voice.
