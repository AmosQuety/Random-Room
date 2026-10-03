# Game platform research: the `game-playground` ecosystem and The Playground

Status: **draft in progress** (sections 1-3 first draft; later sections are filled in as the deep reads finish).
Date: 2026-10-03. Branch: `research/game-platform-report`. Progress log: `progress.md`. Per-repository notes: `notes/`.

## 1. Executive summary (draft)

`gamemann/game-playground` is one Godot 4 physics-sandbox game (MIT) built on about forty small "dot-*" addon
repositories under `modcommunity` (all MIT), plus four other demo games. It is a real-time 3D platform for dedicated
community servers, not a party-game platform. It rotates **maps** inside one game; changing the **game** on a server
is an operator action in `dot-server`, and voting over games exists in `game-hungario` (to be confirmed in Phase 4).

Early findings (to be expanded):
1. Every game ships a small descriptor (`game.yml`) and a second copy of it lives in the deployment repository; a test
   fails when the two disagree (`game-playground/game.yml:13-15`).
2. Test suites declare how many checks and sections they expect and fail if fewer ran
   (`game-playground/examples/headless_playground.gd:52-58,128-152`).
3. Guards are "armed": broken on purpose to prove a test catches them (game-playground `CLAUDE.md`, many sections).
4. game-simple-lobby is not a multi-game lobby: it is the staging room a server runs between games; its module
   outlives a game change and rebinds to the next world (`game-simple-lobby/game/room_module.gd:296-356`).
5. Host migration is chosen per game, with a written reason (`game-playground/game/playground_party.gd:14-20`).

## 2. Scope and method (draft)

Read as text only; nothing from the studied repositories was executed. Repositories were cloned with
`git clone --depth 1` into a scratch folder outside this repository. See `progress.md` for the read lists, the
repositories found but not read, and names referenced in the notes that do not exist.

## 3. Repository inventory (draft)

See the table in `progress.md` ("Repository inventory"); it will be copied here, finished, at the end.
