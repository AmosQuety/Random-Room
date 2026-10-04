# dot-server-deploy

- URL: https://github.com/modcommunity/dot-server-deploy. Commit `2e495af`, 2026-10-03. Licence: MIT. 2.2 MB, 205 files.
- Read: `CLAUDE.md` (884 lines) lines 1-50, 101-325, 326-409 (bug list, partly), 691-718; `cfg.example/vote.yml`,
  `cfg.example/groups.yml` (head), `content/playground/game.yml`; `host/tmc_content.gd` (grep of fields read);
  `examples/install_descriptor.gd` (header, constants, `_test_repositories`); `examples/multigame.gd` (header, section
  list).
- NOT READ: `README.md` (683 lines) beyond its first paragraph; the host scripts' bodies other than the grep; the client
  shell; deploy scripts; most example suites; CLAUDE.md 410-690 and 719-884.

## Purpose

"TMC's server tool: a Godot project that boots a dot-server from `cfg/` and `content/`, and the client shell it
exports — for a browser, and now for a desktop" (`CLAUDE.md:3-4`). "Everything in the family had been run and nothing
had been deployed" (`CLAUDE.md:9`). **This is the one place in the ecosystem where a server runs several different
games and players vote between them** ("This is the first place in the family anybody votes for a game",
`CLAUDE.md` "The players choose the next game").

## How games are discovered, installed, versioned, listed (A2)

- A game is two halves: the **pack** on a content origin (signed, content-addressed, mounted at
  `res://dot_cloud/<id>/<version>/`) and the **descriptor** `content/<id>/game.yml` (which scene the server runs, the
  module, player count, cvars). Only the pack used to be published; `tools/index.gd` now publishes `games.json` plus
  `descriptors/<id>/game.yml`, and `tools/install_games.gd` fetches the descriptors a box asked for and then the
  distinct packs (`CLAUDE.md` "A server installs its games").
- Keyed by **directory id**, not content id: five hungario modes are five descriptors over one pack (same section).
- Version and content id are **stamped by the installer** from the pack it verified (`examples/install_descriptor.gd`
  header lines 3-9; `game-playground/game.yml:8-10`).
- An allow list filters the content directory once, before descriptors are built, because four things read the set:
  the games listing, `changelevel`, the vote menu and the boot game (same section).
- Operators' files are never overwritten (`--refresh` needed); `--games-mode` default `hide`, never delete what an
  operator wrote (same section).
- Verified manifests are kept so a server restarts with the content origin down (same section).
- Fields read from `game.yml` (`host/tmc_content.gd`): `name` (219), `version` (220), `max_players` (221), `kind`
  (223), `scene` (224), `client_scene` (225), `content_id` (235), `dependencies` (277), `cvars` (307), `module` (327),
  `metadata` (353). The descriptor's `metadata.vote` and `metadata.map_vote` were **dropped silently until
  2026-09-23** (`CLAUDE.md` vote section).

## Game vote (B4)

`cfg/vote.yml` is a `DotVoteRules`; `host/tmc_vote.gd` is only the wiring (who is a player, who is an admin, where
announcements go). Template values (`cfg.example/vote.yml`): `vote_exclude: [lobby]`, time limit 2,400 s, ballot 180 s
before the end, warnings at 600/300/60/30 s, 10 s countdown, rock-the-vote at 60% after 300 s with at least 2 players,
one nomination per player, up to 8 nominations, 3 reserved nomination slots, 5 options, fill least-recently-played,
extend 600 s up to 3 times, "keep" not on the ballot, 30 s ballot, **close when all have voted**, plurality, ties by
nomination order, no quorum, cooldown 1 play, apply at end of time, 5 s delay.

Decisions specific to this deployment (`CLAUDE.md` vote section):
- **The lobby is not on the ballot** (`vote_exclude: [lobby]`); it is the home screen, not a game.
- **A game's time limit lives in its own `game.yml`** (`metadata: vote: time_limit_sec:`), not in a second table.
- `begin_on_apply` off; the host's `game_loaded` is the one signal that records a play, because it also fires for a
  manual `changelevel`; `examples/multigame.tscn` fails if flipped.
- Commands are prefixed `game_` (`!game_nominate`, `!game_rtv`, `!game_vote 2`, `!game_timeleft`, `!game_next`)
  because a loaded game may run its own map vote with `rtv`/`nominate`; the first registration of a name wins
  silently, and the game's names are unregistered on the first `changelevel` away.
- A late joiner is re-sent the current vote line with what is left on it.
- The HUD line is cleared by **polling** state after every `advance`, because cancelling a countdown emits nothing.
- A game whose map clock is off must also turn off its vote's clock; `selftest` fails otherwise
  (and see game-playground's `game.yml` `metadata.map_vote`).

## Multi-game switching (B5, B6)

`examples/multigame.gd` (header lines 16-35): drives the real `TmcHost`; three games registered from `content/`;
lobby -> hungry unloads one module and loads another; **between hungry's two modes the same module stays and rebinds**,
"because reloading would drop every connected player"; back to the lobby reloads the lobby module; the client is told
which game it is now. Sections: booting, `+map`, "what the server can run", "lobby -> hungry", "hungry classic ->
frenzy", "hungry -> lobby", "a game that is not there", "the players vote for the next game" (lines 164-482).

`INFERRED` from this and dot-server: the unit "a play of a game" is a **loaded game scene on a server**; setup is the
game's descriptor + cvars + metadata, owned by the server's `content/` directory, not by a play. Scores live in the
game's world and its stats/leaderboards; nothing aggregates across games on a server.

## Roles in, flags out (C7)

Operators write **roles** (groups in `cfg/groups.yml`); dot-server checks **flags**; `TmcAdmins` translates and is a
source merged with others. "It does nothing without authentication ... a guest uid is a random per-device string, so
granting anything to one grants it to anyone" (`CLAUDE.md` "Roles in, flags out"). Three group flags in the shipped
file (`warn`, `announce`, `change`) matched no real flag and granted nothing silently; unknown flags are now named at
boot (`cfg.example/groups.yml` comment).

## Config (F13)

YAML in `cfg/` compiles to a `.cfg` that dot-server's own console executes in the startup slot: "The console is the
parser, the validator, the range clamp, the permission check and the audit trail, and a second one here would drift
from it silently" (`CLAUDE.md` "The configuration is a surface"). The compiled file is written to disk so an operator
can see "what did my configuration actually become". `TmcYaml` is a deliberately small YAML subset that refuses tabs,
duplicate keys, anchors, aliases, tags, block scalars etc. with a file and line (same section). `cfg.example/` is
tracked and `cfg/` is generated and gitignored, after a generated RCON password reached a public repository
(`CLAUDE.md` "`cfg/` is written by setup").

## Contract test between two descriptions of a game (F14, D11)

`examples/install_descriptor.gd`: `REPOSITORIES` maps each game repository to its `content/` directory and
`MUST_AGREE := ["name", "scene", "client_scene", "module"]` (lines 116-126). **But when the sibling checkout is absent
the check passes**: `_check(true, "%s: no sibling checkout, skipped")` (lines 138-141). So in any environment without
the sibling repositories next to it, the "two descriptions agree" test is green without comparing anything. This
contradicts the family's own rule ("fail loudly rather than being skipped", dot-vote `CLAUDE.md` "Validating"). I did
not check whether CI checks out the siblings (`ci.yml` line 40 is a plain `actions/checkout@v4`; NOT READ further).
By reading the five `game.yml` files as text I confirmed the four fields currently agree for arena, lobby, playground,
g2gfast and hungario/hungry_classic.

## Bugs, selection (`CLAUDE.md:326-409`, 691-718)

| # | Bug | Lesson | Our risk |
|---|-----|--------|----------|
| D1 | Web export shipped map manifests but no geometry; client spawned into sky | verify the built artefact, not the filter | Low |
| D2 | Audit log never opened in any default configuration; "no administrative action was ever recorded" | a warning on every boot reads like an unset option | **Check**: our `RoomAuditEvents` is written in the same transaction as the action (`DECISIONS.md` "PIN recovery"); tests exist (`HostRecoveryTests.cs`, `SeatResetTests.cs`) |
| D3 | `./server check` printed `selftest ok` over a module that never compiled | a check must fail when its subject failed to load | Low |
| D4 | Server side of a join never checked; a probe "said it did not happen" | assert on the server's state, not the client's own | **Yes**: our frontend tests mock the API; nothing asserts the server saw a client action end to end |
| D5 | Two lists (addons, games) went stale in one afternoon; a registered game could not be listed or voted for | one list | **Yes**: our games are listed in `Program.cs` and in `frontend/src/games/registry.ts`; nothing checks the two sets match (grep: `frontend/src/games/pollGames.test.tsx:134-136` checks only that client keys are unique and registered) |
| D6 | Two games' files with the same name overwrote each other when flattened | refuse collisions at build | Low |

## What transfers

- The structure of a multi-game server: games are registered from descriptors; one list feeds listing, switching,
  voting and the default; the home screen is excluded from the ballot; per-game settings live with the game.
- Prefixing room-level commands so they cannot collide with a game's own.
- Re-sending the current vote state to a late joiner; polling state for the HUD rather than trusting a cancel signal.
- A contract test between two descriptions of one game, but **failing** when it cannot compare.
