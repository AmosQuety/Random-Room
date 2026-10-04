# game-simple-lobby

- URL: https://github.com/modcommunity/game-simple-lobby. Commit `f96b7ee`, 2026-09-30. Licence: MIT
  (`LICENSE` lines 1-3, "Copyright (c) 2026 game-simple-lobby contributors"). Size 1.7 MB, ~20,000 lines.
- Read: `README.md`, `game.yml`, `CLAUDE.md` lines 1-63, 204-262, 458-626, 721-801 (of 801), `.github/workflows/ci.yml`,
  `game/room_module.gd` 290-356, `game/room_party.gd` (grep), `game/room_player_stack.gd` 10-40,
  `examples/dedicated.gd` 919-966.
- NOT READ: the furniture/level sections of `CLAUDE.md` (264-457), the client, renderer, bridge and props code, the
  suites beyond the game-change section.

## Purpose (answers B5)

**It is not a lobby that hosts several games.** It is a small 2D chat room that "a server runs when it is not running
anything else ... the staging area a player lands in, and the place they wait while an operator changes the game under
them" (`README.md` "The Staging Area"). It has "No scoring, no rounds, no combat, no items" (`CLAUDE.md:20-23`).

How a lobby picks, starts and ends a game: **it does not.** The game on a server is changed by the **operator** through
dot-server's game manager (`changelevel` / `changegame`; `_server.games.change_game(game_id, reason)` in
`examples/dedicated.gd:965`). The lobby's module is "written to outlive a game change": the netcode, props and chat
services belong to the module, not to the world, so when the server swaps the scene the people stay connected and the
module rebinds to the next world, or goes idle if the next game has no room (`game/room_module.gd:296-356`).

What carries between games: the connection, chat, moderation marks about the person (blind and beacon carry; noclip,
about the body, does not) (`CLAUDE.md:61,76`; `dedicated.gd:950-958`), and the **side and class** a player picks in
the lobby: "Picking a team in a lobby and having it mean something in the match you go on to is the whole reason a side
outlives a match" (`game/room_player_stack.gd:23-25`). Per game: the world/scene. Scores: none here (no scoring).

`INFERRED`: the "next game" decision in this ecosystem is either the operator's console command or a dot-vote ballot
over games (game-hungario); the lobby itself has no ballot.

## Descriptor

`game.yml` has the same fields as game-playground minus `cvars`/`metadata`: `content_id`, `version`, `kind`, `name`
("The Room"), `scene`, `client_scene`, `module`, `max_players: 64` (`game.yml:17-27`). Same contract test with
`dot-server-deploy/content/lobby/game.yml` (`game.yml:13-15`).

## Delivered as a pack

`tools/publish_room.tscn` packages `game/` and `scenes/` into a **signed dot-cloud pack**: `manifest.json` and a
content-addressed `objects/` tree that any web server can serve. A mounted pack's `class_name` globals are not
registered in the host, so every script uses relative `preload`, and the tool **refuses to publish** if any staged
script declares a `class_name` (`CLAUDE.md:258-262`). It is the first game "delivered rather than shipped"; a generic
browser client shell downloads it first (`README.md` "What it is for"; `CLAUDE.md:14-18`).

## Hosting and host migration (C8)

`RoomParty` uses dot-peer-to-peer with `max_peers = 8`, `HOST_AUTHORITATIVE`, `migrate_host = true`
(`game/room_party.gd:61-66`). Reasons: nothing to cheat at, small, "Come and sit in a room with me" should not need a
rented server (`CLAUDE.md:721-737`). Contrast: game-g2gfast refuses it because "a host who can cheat and a leaderboard
are one exploit rather than two features" (`CLAUDE.md:733-735`). A browser tab cannot listen, so there is no Host
button in the browser build (`CLAUDE.md:795-797`).

## Identity (C9)

Optional identity stack (dot-user, dot-user-avatar, `DotPlatformHub`); "A LAN room somebody runs for an evening has no
accounts, and that is the most common deployment there is" (`CLAUDE.md:209-212`). Guests get profiles
(`allow_guest_profiles` on) (`CLAUDE.md:214-215`). Avatars arrive after the person, as their own event, so a cosmetic
never holds someone at the door (`CLAUDE.md:217-220`). Entitlements default to nothing (`CLAUDE.md:226-229`).
Punishments are keyed by a uid that outlives the session; chat by session (from game-playground notes, and
`CLAUDE.md:109` heading "The chat key is not the punishment subject").

## Spectating

A living occupant may watch and roam; the departing occupant must be removed before the spectator picks a replacement,
or it picks the person who just left (`CLAUDE.md:745-767`).

## Testing (G15)

Six suites; the list "lives in `.github/workflows/ci.yml` and nowhere else"; `tools/check.sh` reads it and fails if
`release.yml`'s differs. Before that, CI auto-detected `examples/headless_*` so the two real-server suites never ran in
CI (`CLAUDE.md:576`; `ci.yml` comment). `sandbox` runs **a real server and two real clients over real sockets in one
process** ("the one that matters"); three of the bugs below are its (`CLAUDE.md:581-588`). Counts in CLAUDE.md table:
152, 24, 105, 89, 143, 83; README says 84, 24, 88, 88, 143, 82, total 484 (**doc drift** between `README.md`
"Validating" and `CLAUDE.md:565-574`).

## Bugs it found (`CLAUDE.md:458-559`)

| # | Bug | Cause | Lesson | Our risk |
|---|-----|-------|--------|----------|
| L1 | Inputs arrived late on any >30 ms link | clock lead omitted half the round trip; loopback had zero latency | test with a delaying transport | No (no prediction). But our tests never add latency between REST response and SignalR push |
| L2 | RTT stats never written | nothing called `note_rtt` | producer missing | No |
| L3 | Peer map written after the world was told; entities owned by nobody | ordering of synchronous signal | write state before announcing | **Analogue**: we must commit before broadcasting; our endpoints publish after the service call returns (`RoomEndpoints.cs:52-61`) |
| L4 | World scene never had `setup()` called | lifecycle assumed by nothing | make setup idempotent and automatic | Low |
| L5 | Descriptor named an absolute client scene; client sat in LOADING and timed out | descriptor not validated against the delivery shape | validate descriptors | Low |
| L6 | Message ids sorted by pointer differed between processes | test ran both ends in one process | test across processes | **Analogue**: our backend and frontend are separate programs; contract between them (`GameType` strings, payload shapes) is not tested across the boundary |
| L7 | Leave broadcast sent to the peer that had left | ordering | remove before announcing | Low |
| L8 | Module never moved onto the next world after a game change; room froze | override never written; nothing changed game in tests | **test the game change** | **Yes for the plan**: switching games in a room needs its own test that a room keeps working across the switch |
| L9 | Seat chooser never read seats | policy mode ignored the callback | screenshot found it | Low |
| L10 | A non-numeric punishment length became permanent | parser default 0 meant "permanent" | refuse what you cannot parse | **Analogue**: any default that means "forever" (our retention `0` = off is documented, `README.md` "Data retention") |
| L11 | Test runs wrote 61 gags into the real punishment file | shared default path | inject paths | No |
| L12 | Server suites hung on stdin console | reader thread blocks | turn off interactive input in tests | No |

## What transfers

- A module that **outlives the game** and rebinds to the next one is the closest thing in the ecosystem to our "switch
  games inside a room": people, chat and moderation belong to the server/module; the world belongs to the game.
- The list of suites lives in one place and a check fails if a second copy differs.
- A test that drives the real switch (game change and back) with people in the room.
- Choice made in the lobby (side) carried into the next game.
