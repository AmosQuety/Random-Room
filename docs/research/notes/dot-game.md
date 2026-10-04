# dot-game

- URL: https://github.com/modcommunity/dot-game. Commit `5603869`, 2026-09-27. Licence: MIT. 276 KB, 36 files.
- Read: `CLAUDE.md` lines 1-70, 120-140. NOT READ: code, "DotGameServices", "live tools" and "hooks" sections.

## Purpose

"The wiring every server game repeats" (`CLAUDE.md:3`): a `DotGameModule` base (netcode, identity, services, roster,
tick, teardown) and `DotGameServices` (chat, moderation, voice). It "holds no rules": rounds, teams, objectives and
winners are dot-match's and dot-objective's (`CLAUDE.md:23-26`).

## Why it exists (the evidence)

Five game modules of 837-1,816 lines were mostly renamed copies; **two of the five read `peer_id` from a spawn event
that carries only `userid`, so "nobody ever joined those dedicated servers, silently"** (`CLAUDE.md:9-21`). "The
argument for a base class here is not tidiness, it is that there is one line to be right." After conversion,
game-simple-lobby's module is about 90 lines for the mg-* games and the lobby kept ~487 lines of its own code
(`CLAUDE.md:125-127`; `game-simple-lobby/CLAUDE.md:30-36`). The test for what belongs in the base: "would every game
write it the same way with only the class names changed?" (`CLAUDE.md:27`).

## Fatal versus not (A1: what a game must provide)

`_module_load` is fatal only if there is no game object or the netcode will not start; identity or services failures
are logged ("A server where everybody is a guest is a configuration"); a refused `_game_load()` is fatal and unwinds
(`CLAUDE.md:33-43`). Teardown runs in reverse: signals, roster, services, identity, bridge, manager (`CLAUDE.md:45-49`).
A client whose message schema cannot play is disconnected with a reason saying who must update (`CLAUDE.md:57-59`).

## Transfer

Our `IGameEngine` + shared primitives already play this role, and our `RoundGameEngine` is the "base for games that
would write it the same way" (`README.md` "Adding a game"). The lesson that does transfer: when a lifecycle hook is
copied into many engines, a single wrong line is copied too; the plan's change from `GetSetupAsync(roomId)` to
`GetSetupAsync(sessionId)` across 19 engines is exactly that kind of edit, and a shared helper is safer than 19 hand
edits. `INFERRED`.
