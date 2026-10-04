# Game platform research: the `game-playground` ecosystem, compared with The Playground

Date: 2026-10-03. Branch: `research/game-platform-report`. Companion files: `progress.md` (log, inventory, assumptions),
`notes/*.md` (one file per repository studied, with read and NOT READ lists), `notes/our-app.md` (our baseline).

Paths in this report are relative to each repository's root. "their" = the studied repositories; "ours" = this
repository. Every finding is numbered (F1, F2, ...) so it can be referred to. A claim marked `INFERRED` is my
reasoning, not something a file says; `NOT READ` means I did not read it.

---

## 1. Executive summary

**What the ecosystem is.** `gamemann/game-playground` (MIT) is one Godot 4 first-person physics sandbox. It sits on
top of about forty small `dot-*` addon repositories under `modcommunity` (all MIT), next to four other demo games and a
deployment repository (`dot-server-deploy`) that runs a dedicated server hosting several games. It is a real-time 3D
platform for community-run dedicated servers, much like the classic modded shooter servers. It is **not** a party-game
platform. Every README says it was built with an AI coding assistant by one maintainer and is "partially tested", with
"very little of this ... in front of real players yet" (`game-playground/README.md`, "From Maintainer & WARNING"). Every
commit I saw is from September and October 2026, and one repository changed while I was reading it (F27).

**The ten findings that matter most for us:**

1. **game-playground rotates maps, not games** (F2). The ecosystem does switch games inside one server, but that lives
   in `dot-server` and `dot-server-deploy`, where an operator command or a player vote changes the running game while
   everyone stays connected (F3, F4).
2. **The pattern that makes switching work is "people outlive the game".** Connections, chat, moderation and the vote
   belong to a long-lived server module. Only the game world is freed and replaced. Rebuilding the people layer "is a
   disconnect for everybody" (F5). For our data model, this points to option A in our plan (the session owns its game
   and setup; the room keeps the people), not to linked rooms (§9).
3. **Every part of game switching that had never been run end to end was broken**, and none of it raised an error: six
   bugs in dot-server and a frozen room in the lobby (F6). The case that slipped through was a client that was still
   joining when the switch happened.
4. **The "next game" ballot is built from the same registry that launches games.** It filters by player count, keeps
   the home screen off the ballot, and refuses loudly if the ballot would be empty (F7, F8). The vote engine
   (`dot-vote`) has 80 settings and **no veto** (F9). A friends' room needs perhaps five of those settings.
5. **Each game has a small descriptor (`game.yml`), and a test compares it with the second copy kept by the
   deployment** (F10). But that test **passes without comparing anything** when the other repository is not checked
   out next to it (F11). Their rule that "a second copy of a number is a second number that can disagree" is applied
   in at least ten places in the code (F12).
6. **Their test suites state how many checks and sections they expect, and fail if fewer ran** (F13). Guards are
   "armed": someone breaks the guard on purpose to prove the test catches it. That step is done by hand and written
   up in prose, not automated (F14).
7. **Bots play the game in the integration suites**, and the suites that mattered most run a real server and two real
   clients over real sockets (F15). Most bugs they found were in the joins between parts, not inside the parts (F16).
8. **Whether to hand over the host role is decided per game, with a written reason** (five games, two answers). A
   host who could cheat is never allowed to file persistent results (F17).
9. **Permissions are flags plus immunity, not roles.** Operators write roles, and those are translated to flags in one
   place. Every command source goes through one permission check (F18).
10. **We already match or beat them** on hidden information (we send per-viewer snapshots, they rely on owner-only
    replication), on server-owned timers, on restart safety, and on test isolation (F19, F20).

**The three biggest risks of copying anything:**

- **Copying for scale we do not have.** Most of what is here (80 vote settings, flags with immunity, signed content
  packs, a versioned binary wire, acked baselines) exists because strangers share long-running public servers. Our
  rooms are short-lived groups of friends with a host.
- **Copying from code that has hardly met real players.** The authors say so themselves (F1). The lessons are well
  argued, but most were found by the authors' own test suites, not by players.
- **Treating "they do it" as proof.** Several of their safeguards had holes I could see (F11, F25, F26). Copy the
  reasoning, then test it ourselves.

**Recommended next step:** Before changing any data model, add contract tests that compare the server's and the
frontend's copies of each game's facts: the set of game keys, the player ranges, and the setup limits. Make these
tests fail when they cannot compare, never skip (roadmap step 1). Then build a small bot that plays games over the real
API (step 2). Both are cheap. Both protect the larger change in our plan (`docs/plan-switch-games-in-a-room.md`).
When that change comes, take option A.

---

## 2. Scope and method

**How.** I read repositories as text only. I cloned each with `git clone --depth 1` into a scratch folder outside this
repository. I ran nothing from them. Our own project I read but did not build or run. Started at 21:26 by the
container clock. The finishing time is in `progress.md`. The container clock showed far less elapsed time than the
work took, so treat the times there as approximate.

**Read deeply** (notes files exist): game-playground, game-simple-lobby, game-hungario, dot-vote, dot-server,
dot-server-deploy. **Read in part**: dot-core, dot-game, dot-net, dot-peer-to-peer, dot-user, dot-stats,
dot-leaderboard, dot-achievements, dot-moderation, dot-ci, dot-cloud, dot-map, dot-match. **Skimmed**: everything else
listed in §3 (`notes/skimmed.md`).

**NOT READ** (most important gaps):
- The "family-wide" `../../CLAUDE.md`, `docs/testing.md` and `docs/gdscript-hazards.md` that every repository refers to.
  They live in a workspace outside any repository I could clone.
- The code bodies of `dot_vote_director.gd` (1,884 lines), `dot_vote_ballot.gd`, `dot_vote_clock.gd`, dot-server's
  server, console and RCON, and dot-net's code. I described their behaviour from each repository's own `CLAUDE.md` and
  README, which are detailed. Where a document and the code could disagree, I checked the code only where noted.
- game-playground's 3D gameplay code, maps, vehicles, NPCs and client, and about half of its `CLAUDE.md`, which covers
  maps and movement.
- The README of dot-server-deploy (683 lines) beyond its first paragraph.

**UNREACHABLE.** These names appear in the notes, but `git ls-remote` found no such repository under either owner:
dot-query, game-blob, website-city, game-dev, dot-randomness, dot-p2p. The TMC "backbone" web service (stats and
leaderboard storage, sign-in) is closed source by every README's own statement. So server-side validation of submitted
records is **not visible**.

**BLOCKED.** `moddingcommunity.com` (not tried, per the rules). `codeload.github.com` and `api.github.com` for other
repositories (the preflight found them blocked; I did not retry).

**Confidence by section:**

| Section | Confidence | Why |
|---------|------------|-----|
| 4 Platform | Medium-High | Main mechanisms read in the code (descriptor, module, vote wiring, party, config). Engine internals came from docs. |
| 5 Comparison | High for ours, Medium for theirs | I reopened our files. Their side is partly from docs. |
| 6 Bugs | High | Quoted from their notes. "Our risk" judgements are mine. |
| 7 Ideas | Medium | Reasoned from evidence; marked `INFERRED` where so. |
| 9 Roadmap | Medium | Depends on owner decisions (§10). |
| 11 Unexpected | High | Each item is checked against a file. |

---

## 3. Repository inventory

All licences are MIT (first line of each `LICENSE`). Size is the working tree without `.git`, then the number of
tracked files. Status: READ DEEP, PART (sections read), SKIMMED (README only), NOT READ.

| Repository | Purpose (own words, from README) | Commit / date | Size | Status | Notes |
|------------|----------------------------------|---------------|------|--------|-------|
| [gamemann/game-playground](https://github.com/gamemann/game-playground) | first-person physics sandbox with timed courses; the place the addons meet | a43a116 / 10-03 | 38 MB / 298 | READ DEEP | game-playground.md |
| [modcommunity/game-simple-lobby](https://github.com/modcommunity/game-simple-lobby) | 2D chat room a server runs between games | f96b7ee / 09-30 | 1.2 MB / 104 | READ DEEP | game-simple-lobby.md |
| [modcommunity/game-hungario](https://github.com/modcommunity/game-hungario) | agar.io-like arena, six modes registered as six games | 63e06cf / 10-03 | 8.3 MB / 164 | READ DEEP | game-hungario.md |
| [modcommunity/dot-vote](https://github.com/modcommunity/dot-vote) | vote for what plays next: games, maps or modes | e7b0c5e / 09-26 | 536 KB / 49 | READ DEEP (docs), PART (code) | dot-vote.md |
| [modcommunity/dot-server](https://github.com/modcommunity/dot-server) | dedicated server: console, RCON, permissions, game switching | 154bc60 / 10-02 | 1.1 MB / 85 | READ DEEP (docs), PART (code) | dot-server.md |
| [modcommunity/dot-server-deploy](https://github.com/modcommunity/dot-server-deploy) | deployment: multi-game server, game vote, installer, client shell | 2e495af / 10-03 | 2.2 MB / 205 | READ DEEP (parts) | dot-server-deploy.md |
| [modcommunity/dot-core](https://github.com/modcommunity/dot-core) | foundation: config layering, results, conventions | fcce93f / 09-26 | 696 KB / 92 | PART | dot-core.md |
| [modcommunity/dot-game](https://github.com/modcommunity/dot-game) | base module every server game repeats | 5603869 / 09-27 | 276 KB / 36 | PART | dot-game.md |
| [modcommunity/dot-net](https://github.com/modcommunity/dot-net) | netcode: snapshots, prediction, interest, versioned wire | b26b6d0 / 10-03 | 720 KB / 67 | PART | dot-net.md |
| [modcommunity/dot-peer-to-peer](https://github.com/modcommunity/dot-peer-to-peer) | player-hosted sessions, join codes, host election | 37d47c1 / 09-24 | 196 KB / 27 | PART | dot-peer-to-peer.md |
| [modcommunity/dot-user](https://github.com/modcommunity/dot-user) | profiles with per-server pseudonymous ids | 565cba7 / 09-24 | 248 KB / 32 | PART | dot-user.md |
| [modcommunity/dot-stats](https://github.com/modcommunity/dot-stats) | per-player statistics declared once | 5a2519c / 09-26 | 208 KB / 28 | PART | dot-stats.md |
| [modcommunity/dot-leaderboard](https://github.com/modcommunity/dot-leaderboard) | boards scoped by string keys | adf3797 / 09-24 | 216 KB / 30 | PART | dot-stats.md |
| [modcommunity/dot-achievements](https://github.com/modcommunity/dot-achievements) | achievements as data | 6e213da / 09-24 | 272 KB / 36 | PART | dot-stats.md |
| [modcommunity/dot-moderation](https://github.com/modcommunity/dot-moderation) | bans, gags, mutes as one durable record | e869fad / 09-24 | 444 KB / 45 | PART | dot-moderation.md |
| [modcommunity/dot-ci](https://github.com/modcommunity/dot-ci) | shared CI and release workflows | d26b5a4 / 10-03 | 112 KB / 12 | PART | dot-ci.md |
| [modcommunity/dot-cloud](https://github.com/modcommunity/dot-cloud) | signed, content-addressed content delivery | c88670e / 09-30 | 676 KB / 67 | PART | dot-cloud.md |
| [modcommunity/dot-map](https://github.com/modcommunity/dot-map) | map catalogue, rotation, simple vote | 42e45fc / 09-25 | 392 KB / 39 | PART | dot-map-and-dot-match.md |
| [modcommunity/dot-match](https://github.com/modcommunity/dot-match) | match state machine, scoreboard | 1cd016f / 09-27 | 288 KB / 40 | PART | dot-map-and-dot-match.md |
| dot-chat, dot-inventory, dot-timer, dot-combat, dot-loadout, dot-npc, dot-npc-ai, dot-player-controller, dot-props, dot-ui, dot-user-avatar, dot-vehicle, dot-physics, dot-spawn, dot-player, dot-player-char, dot-player-class, dot-team, dot-platform, dot-auth (modcommunity); game-arena, game-g2gfast (modcommunity); zee-dot-weapons (gamemann) | see `notes/skimmed.md` | see `progress.md` | | SKIMMED | skimmed.md |
| dot-2d, dot-objective, dot-bootstrap, dot-voice, dot-audio, dot-settings, dot-weapon, dot-browser, dot-spectate, dot-2d-hungry, dot-fx (modcommunity); mg-buses-from-hell, mg-smash-copter (gamemann) | exist (`git ls-remote` succeeded) | | | NOT READ | |
| dot-query, game-blob, website-city, game-dev, dot-randomness, dot-p2p | named in notes; not found | | | UNREACHABLE | |

---

## 4. How the platform works

```
                    operator console / RCON / chat "!cmd"  ──►  one DotConsole.execute + permission flags
                                                                    │
  content origin (signed packs)  ──►  installer writes content/<id>/game.yml  ──►  DotGameDescriptor list
                                                                    │                 │
                                                       games listing, changelevel, vote ballot, boot game
                                                                    │
  ┌──────────────────────────── dot-server process (long-lived) ────────────────────────────┐
  │  sessions (CONNECTING→AUTH→DOWNLOADING→LOADING→SPAWNED, and SPAWNED→DOWNLOADING = a switch)│
  │  modules (outlive games): chat, moderation, identity, vote, netcode bridge, game module  │
  │        │ rebind on _module_game_changed                                                  │
  │  current game scene (freed and replaced on change)  — world, match, map rotation         │
  └──────────────────────────────────────────────────────────────────────────────────────────┘
        │ snapshots + events (dot-net)            ▲ inputs and asks (never state)
        ▼                                         │
     clients (browser or desktop shell; mount packs; predict own player)
```

**The game contract (A1).** A game provides: a descriptor (`game.yml`); a server scene; optionally a client scene; and
a server module (`DotModule`) that registers commands and cvars, hooks events, builds its netcode and services, and
refuses to load when it cannot run a game (`game-playground/game/playground_module.gd:126-138`; `dot-game/CLAUDE.md:33-43`).
There is no separate "rules" or "per-player view" interface in the contract. Rules live in game code and in addons such
as dot-match. Per-player views come from the netcode's audiences and interest. Shutdown is the module's unload, which
must undo what it registered (`dot-server/CLAUDE.md:652-662`). `game.yml` fields and readers: see §5-A.

**Discovery and installation (A2).** Two halves: a signed pack on a content origin, and a descriptor. `tools/index.gd`
publishes `games.json` plus the descriptors. `./server install-games` fetches the requested descriptors and packs,
and stamps `content_id` and `version` from the pack it verified. An allow list filters the content directory once, so
listing, switching, voting and the boot game all see the same set (`dot-server-deploy/CLAUDE.md`, "A server installs
its games"). Releases are tags. dot-ci builds a zip or tarball plus a pack zip with SHA-256 sums, and never signs or
publishes (`dot-ci/README.md:83-99`).

**Rooms, servers, lobbies (B5, B6).** The unit is a **server running one game at a time**. The lobby is itself a game:
the room people wait in while the operator or a vote changes the game (`game-simple-lobby/README.md`). A "play" of a
game is the lifetime of its loaded scene. Its setup is the descriptor plus cvars plus metadata, owned by the server's
content directory, not by the play (`INFERRED` from `dot-server-deploy/host/tmc_content.gd:219-353` and
`dot-server/CLAUDE.md:476-481`).

**Rotation and voting (B4).** The rotation (dot-map) picks the next item, with a cooldown. dot-vote lets players
override it with rock-the-vote, nominations, a ballot, extend, tie-breaks, a quorum and cooldowns. One engine serves
maps (game-playground), modes registered as games (game-hungario) and whole games (dot-server-deploy). It works
because a choice is "an id, a name, and the handful of facts a ballot needs" and a source says what applying it means
(`dot-vote/CLAUDE.md`, "The one design decision").

**Hosting and roles (C7, C8).** On a dedicated server: flags plus immunity, checked in one console entry point
(`dot-server/CLAUDE.md:157-206`). In player-hosted (P2P) sessions: a deterministic host election, and a per-game choice
whether to hand over the host role at all (`game-playground/game/playground_party.gd:14-20`).

**State sync (D10).** Clients send inputs and requests, never state. The server sends snapshots (owner-only fields
stay off other players' wires) and events. Joiners get nothing until they say READY. Sub-protocols such as the
inventory use sequence-numbered ops with cumulative acks and whole-document resync (§5-D).

**Persistence and stats (E12).** Records, stats, achievements and boards are keyed by a scoped pseudonymous id. Without
an identity layer they are keyed by session. Nothing is filed from a session whose host could cheat (§5-E).

**Operations (F13, F14).** Layered config: defaults < file < environment < command line. Startup-only settings lock
when the listener opens. Invalid files are refused while keeping the previous state. Out-of-range values are clamped.
Console, RCON and chat commands run through one path. One shared CI repository serves about sixty repositories.

**Testing (G15).** Headless scenes run as suites with declared check and section totals. Bots are driven by scripted
commands. A real server and two real clients run over sockets in one process. Guards are armed by hand.

---

## 5. Comparison with our app

Verdicts: THEIRS BETTER, OURS BETTER, EQUIVALENT, NOT COMPARABLE. "Real-time" means the difference exists only because
theirs is a real-time game.

### A. The game contract

**`game.yml`, field by field** (A1). Example from `game-playground/game.yml`. Readers: `dot-server-deploy/host/tmc_content.gd`
(line numbers below) into `DotGameDescriptor` (`dot-server/addons/dot_server/game/dot_game_descriptor.gd:15-87`,
validated at 94-143).

| Field | Meaning | Read at |
|-------|---------|---------|
| `content_id` | pack address; stamped by the installer | tmc_content.gd:235 |
| `version` | stamped by the installer from the verified pack | tmc_content.gd:220 |
| `kind` | `pack` or `builtin` | tmc_content.gd:223 |
| `name` | display name | tmc_content.gd:219 |
| `max_players` | operator-editable | tmc_content.gd:221 |
| `scene`, `client_scene` | server and client scenes, relative to the pack mount | tmc_content.gd:224-225 |
| `module` | the server module | tmc_content.gd:327 |
| `dependencies` | other packs, `<owner>/<name>@<version>` | tmc_content.gd:277; validated in dot_game_descriptor.gd |
| `cvars` | applied when the game loads; two passes because modules register cvars later (dot-server CLAUDE.md:468-486) | tmc_content.gd:307 |
| `metadata.vote`, `metadata.map_vote` | per-game vote settings, layered between code defaults and the operator's file | tmc_content.gd:353; `game-playground/game/playground_vote.gd:168-170` |
| `min_players` | exists on `DotGameDescriptor` (line 82); not in any `game.yml` I read | — |

| Topic | Theirs | Ours | Verdict | Why |
|-------|--------|------|---------|-----|
| What a game provides | descriptor + scene + module | `IGameEngine` (`backend/RandomRoom.Api/Games/IGameEngine.cs:10-59`) + `GameModule` (`frontend/src/games/types.ts:30-52`) | EQUIVALENT | Ours is typed and has explicit per-viewer views and a public preview; theirs has a declarative descriptor. |
| Single description of a game | descriptor, copied in deployment, compared by test (F10, F11) | facts split between engine and frontend module, no comparison (F21) | THEIRS BETTER | They at least compare the copies. We do not. |
| Discovery and versioning (A2) | install from origin, stamped versions, allow list | compiled in (`Program.cs:50-68`, `frontend/src/games/registry.ts`) | NOT COMPARABLE | We ship one app. No need for installs. |
| Content packaging (A3) | signed, content-addressed packs; scripts named by path because packs cannot register class names | embedded versioned JSON (`Games/Shared/ContentBank.cs:7-9`) | OURS BETTER for safety, NOT COMPARABLE otherwise | We never fetch executable content. |

**Why scripts are named by path (A3).** A mounted Godot pack's `class_name` globals are not registered in the host, so
any type reference by class name inside a pack fails to compile. References by `preload`/`extends` path work
(`dot-server-deploy/CLAUDE.md:20-34`; `game-playground/CLAUDE.md:292-299`). The pack format allows scenes, scripts,
imported assets and `requires.json`. It forbids reusing a mount path (packs cannot be unmounted, so versions go in the
path), unsigned manifests by default, and unsafe version strings (`dot-cloud/CLAUDE.md:17-37,121-146`).
game-simple-lobby's publisher refuses a pack containing a `class_name` (`game-simple-lobby/CLAUDE.md:258-262`).

### B. Running more than one game, and choosing what plays next

| Topic | Theirs | Ours | Verdict | Why |
|-------|--------|------|---------|-----|
| Switching game within a session (B4, B5) | server `change_game`; modules outlive the scene and rebind (F3, F5) | room fixed to one game (`Domain/Room.cs` "Fixed for the room's lifetime") | THEIRS BETTER | This is the feature our owner wants. |
| Next-game vote (B4) | dot-vote, 80 settings; ballot from the game registry, player-count filter, home screen excluded (F7, F8, F9) | none | THEIRS BETTER | We have nothing. A small subset is enough. |
| Unit of a play (B6) | a loaded game scene on a server; setup belongs to the server's content | `GameSession` with no game type; setup keyed by room (`Domain/RoomGameSetup.cs`; `GameStore.cs:44-51`) | EQUIVALENT shape, different keying | Both keep setup outside the play. Ours must move it to the session to allow switching (plan A). |
| What carries between games | connections, chat, moderation marks about the person, team/side; not the world (game-simple-lobby `room_module.gd:296-356`; `room_player_stack.gd:23-25`) | everything is per room already (players, PINs, audit) | EQUIVALENT | Our room is already their "module that outlives the game". |
| Scores across games | none; boards scoped per mode (game-hungario `CLAUDE.md` "Boards") | none; per session | EQUIVALENT | Neither aggregates. |
| Testing the switch | `multigame`, `content_switch`, lobby `dedicated` "a game change" | n/a | THEIRS BETTER | They learned the hard way (F6). |

How game-simple-lobby and game-hungario answer the four specific questions:

| Question | game-simple-lobby | game-hungario |
|----------|-------------------|---------------|
| How is the next game chosen? | Not by the lobby. The operator (`changegame`) or the server's game vote in dot-server-deploy | Rotation with cooldown; players override with dot-vote over its six modes; `DotVoteGameSource.apply` calls `change_game` (`game/hungry_maps.gd:277-281`) |
| Shared vs per game | Shared: module, netcode, props manager, chat, voice, moderation, team/class choice. Per game: the world scene (`game/room_module.gd:296-356`) | Shared: net manager, bridge, loadout manager. Per game: the mode scene and its world (`CLAUDE.md:617-626`) |
| Where scores are kept | No scoring (`CLAUDE.md:20-23`) | dot-match per world; boards per mode (`CLAUDE.md` "Boards"); `INFERRED` that the match is lost on change |
| Players between games | Stay connected; re-added to the new world by `RoomBridge.rebind` | Stay connected; respawned in the new world |

### C. People, hosting and roles

| Topic | Theirs | Ours | Verdict | Why |
|-------|--------|------|---------|-----|
| Roles and permissions (C7) | flags + immunity; groups translated to flags; one console path; command flags such as `CHANGEMAP` vs `GENERIC` (F18) | host vs player, checked by name in services (`GameSessionService.cs:162-168`; `RoomAdminService.cs:142,157,182`) | EQUIVALENT for our needs | Two roles fit a friends' room. Their model fits staff teams on public servers. |
| Permission tests | assert each command's flag (`game-playground/examples/dedicated.gd:741-766`) | wrong-actor tests per game (`README.md` "Adding a game" step 4) | EQUIVALENT | |
| Host leaves (C8) | per-game choice to migrate the authority; deterministic election; reporting refused while P2P (F17) | host stays host; recovery code; no hand-over (`DECISIONS.md` "Host recovery code") | NOT COMPARABLE (real-time) | Their "host" is the machine holding the simulation. Ours is a permission. |
| Identity (C9) | scoped HMAC pseudonymous ids; guests may name themselves, accounts may not (`dot-user/README.md:42-75`) | per-room seats with a PIN; host names players | EQUIVALENT | A per-room seat already cannot be correlated across rooms. |
| Bans and moderation (C9) | durable punishment records keyed by a durable subject; expiry checked on read (`dot-moderation/README.md:14-51`) | seat reset between games; delete room; append-only audit | NOT COMPARABLE | Strangers versus friends. |

**The host-migration table** (`game-playground/game/playground_party.gd:14-20`; I verified each game's
`migrate_host` setting in its own party file):

| Game | Migrates | Reason given | Code |
|------|----------|--------------|------|
| game-simple-lobby | yes | "nothing is built, and a host leaving is somebody's evening" | `game/room_party.gd:66` |
| game-hungario | yes | "a continuous arena with no round to be in the middle of" | `game/hungry_party.gd:56` |
| game-arena | no | "the host holds the match clock, the score and every hitbox" | `game/arena_party.gd:61` |
| game-g2gfast | no | "a time made of two machines' clocks is worse than no time" | `game/g2g_party.gd:63` |
| game-playground | no | "the world is the host's physics state and does not move" | `game/playground_party.gd:63` |

### D. State, networking and correctness

| Topic | Theirs | Ours | Verdict | Why |
|-------|--------|------|---------|-----|
| What a client sends | inputs and asks, never state (`dot-net/CLAUDE.md:75-89`) | actions over REST, validated by the engine (`GameSessionService.cs:51-73`) | EQUIVALENT | |
| What the server sends | per-tick snapshots plus events; owner-only fields; interest | a full per-viewer snapshot after every change (`Services/RoomNotifier.cs:51-57`) | OURS BETTER for hidden info, THEIRS for bandwidth (real-time) | We build each player's view on the server. They filter fields. |
| Ordering | tick numbers; for the inventory, per-op sequence numbers with cumulative acks | snapshot `Sequence`, client keeps the newest (`RoomSnapshotSequencer.cs:5-42`; `frontend/src/lib/snapshots.ts`) | EQUIVALENT | Ours is simpler because each push is the whole state. |
| Resync | ask for the whole document when an answer is 3 s overdue; HELLO resets | refetch on every (re)connect (`frontend/src/lib/useRoom.ts:28-59`) | EQUIVALENT | Both treat the whole state as the cure. |
| Ready gate | send nothing until READY (F23) | push only to online players; client refetches after connecting | EQUIVALENT | We avoid their bug by design. |
| Hidden information | `Audience.OWNER`; bag sent through a helper that can only reach one peer | `GetPayloadForAsync(viewer)`; viewer-less view must be safe (`IGameEngine.cs:49-55`) | EQUIVALENT | Both make leaking hard by construction. |
| Version skew | message ids derived from names; old clients refused with a sentence (`dot-net/CLAUDE.md:306-321`) | "unknown game type ... Try refreshing the page" only (`frontend/src/components/RoomScreen.tsx:184`) | THEIRS BETTER | A stale cached bundle after a deploy is not detected. |
| Timers (D11) | one tick source (`sv_tickrate`) feeding everything; tested with a different number at each end | server deadlines, lazy `tick` (`Games/Shared/ServerTimer.cs:4-19`) | EQUIVALENT | Both have one owner of time. |

**Their "one source of truth" rule, every example I found** (D11): (1) tick rate only in `server.cfg`, no
`pg_tickrate` (`game-playground/game/playground_config.gd:7-12`; `CLAUDE.md:758-778,991-993`); (2) loadout catalogue
built from the weapons list (`CLAUDE.md:845-849`); (3) chat channels the UI offers come from the server's routing
(`CLAUDE.md` "The chat box"); (4) server-browser counts read the owners' tallies (`CLAUDE.md` "The server browser");
(5) map geometry and zone files from the same constants, checked by `_test_zone_file_matches_the_map`
(`CLAUDE.md:1143-1160`); (6) every stat an achievement watches must be declared (`CLAUDE.md:922-923`); (7) stats
recorded from existing signals (`CLAUDE.md:917-920`); (8) `game.yml` vs the deployment copy (`game.yml:13-15`); (9) a
stopped map clock must stop the vote clock (`CLAUDE.md:946`); (10) vote cue ids named once
(`game/playground_vote.gd:50-55`); (11) hungario's map catalogue built from game descriptors
(`game-hungario/game/hungry_maps.gd:118-124`); (12) game-simple-lobby's suite list lives only in `ci.yml`, and
`check.sh` fails if `release.yml` differs (`game-simple-lobby/CLAUDE.md:576`); (13) dot-vote's self-test fails if any
of its 80 settings is read by nothing (`dot-vote/CLAUDE.md` "Validating"). How they test it: by giving the two ends
**different** values so a disconnected chain fails (`game-playground/examples/dedicated.gd:464`), by comparing
generated files with their source, and by comparing two lists.

### E. Persistence, stats and progression

| Topic | Theirs | Ours | Verdict | Why |
|-------|--------|------|---------|-----|
| Keys | scoped pseudonymous id, or the session id ("honest") (`game-playground/CLAUDE.md:957-963`) | player name within a room | EQUIVALENT | Both key by an id that lives as long as it should. |
| Recording | declared once with a merge kind; from existing signals; batched (`dot-stats/README.md`) | per-session scoreboards built by each engine | THEIRS BETTER for cross-game | We have no cross-session record. |
| Validation | achievements validated at boot; nothing filed from P2P; server-side backbone validation UNREACHABLE | n/a | NOT COMPARABLE | |
| Replays and records | dot-timer records and replays; playback deliberately left to each game (`game-playground/CLAUDE.md` "Things deliberately not here") | none | NOT COMPARABLE (real-time) | |

**The equivalent for us** (`INFERRED`): a `SessionResult` row per completed session (placing per seat), written at the
one place completion happens (`GameSessionService.cs:66-70,94-95`), and a room-wide board that combines **placings**
across games. That follows their choice to scope boards per mode rather than add raw numbers that do not compare.

### F. Operations

| Topic | Theirs | Ours | Verdict | Why |
|-------|--------|------|---------|-----|
| Config layering (F13) | defaults < file < env < argv; startup-only lock; clamp out-of-range; malformed admin file keeps previous state | appsettings < environment; refuses to start without signing key or migration permission (`README.md` "Deploy on Render", "Database migrations") | EQUIVALENT | Same layering. Our refusal rules are as strict. |
| Invalid config | refused whole, defaults stand, logged (`game-playground/game/playground_vote.gd:164-177`) | refuses to start | EQUIVALENT | |
| Operator tools | `pg_status`, `pg_give`/`pg_inv`, `blind`, `beacon`, gag/mute, `pg_vote`, zone painting, RCON (game-playground notes §4) | host's in-room controls only | NOT COMPARABLE | No operator role in our product. A `status` endpoint could help support (idea N3). |
| CI (F14) | shared reusable workflows; parse + suites with timeouts; read output even on exit 0; tag-based releases with SHA-256 | PR-only CI, tests + lint + build (`.github/workflows/ci.yml`) | EQUIVALENT for our size | |
| Version stamping | tag is the version; pack declares addon API it needs | none | THEIRS BETTER | Would let a client detect a newer server. |

### G. Testing practice (G15)

| Topic | Theirs | Ours | Verdict | Why |
|-------|--------|------|---------|-----|
| Organisation | 5-6 headless suites per game: world alone, net over lossy loopback, real server + module, presentation, real server + 2 clients | per-game xUnit against real Postgres; Vitest per component (`README.md` "Test") | EQUIVALENT | |
| Bots | scripted commands drive a player through a whole map | none | THEIRS BETTER | |
| End to end | real server + two clients over sockets (`game-simple-lobby/CLAUDE.md:581-588`) | none in repo; QA audits outside the repo (`NEXT_STEPS.md`) | THEIRS BETTER | |
| Counting | declared CHECKS and SECTIONS totals (F13) | per-test reporting | EQUIVALENT | xUnit and Vitest already fail per test. |
| Arming | manual, recorded in prose (F14) | none | THEIRS BETTER | Process, not tool. |
| Isolation | user:// state leaked between runs twice (F24) | throwaway database per test | OURS BETTER | |

Counts. Declared in code: game-playground `headless_playground` 563 checks / 31 sections, `headless_net` 294 / 31,
`dedicated` 224 / 25 (`examples/*.gd` constants). game-simple-lobby: 152, 24, 105, 89, 143 and 83 per its `CLAUDE.md`.
Its README says 484 in total and gives different per-suite numbers (F25). They write that the integration suite "has
found nine real bugs, three of them in other repositories; a screenshot found three more"
(`game-playground/README.md` "Validating"). The real bugs are listed in §6.

**What a bot-driven end-to-end suite would look like for our 19 games** (`INFERRED`). The harness: a test host that runs
the real API in process with a real Postgres (we already have one per test), creates a room through
`POST /api/rooms`, claims every invite, and opens one SignalR connection per bot. Each game gets a small bot
"strategy": given the bot's own snapshot, pick a legal action. The suite runs each game from start to `Completed`,
asserts on each player's **pushed** snapshot (not on the action's response), and asserts that no bot's snapshot
contains another player's secret. Count the games and phases visited, and fail if fewer ran. Arm it once by removing
one per-viewer filter and one phase guard, and confirm it fails.

### H. Process (H16)

| Topic | Theirs | Ours | Verdict | Why |
|-------|--------|------|---------|-----|
| Decision log | per-repository `CLAUDE.md`: decision, the alternative rejected, the bug that taught it, the test that guards it, and whether it was armed | `DECISIONS.md` (one line per decision, with reason), `Gaps_Bugs.md`, `NEXT_STEPS.md` | EQUIVALENT | Ours is shorter and readable. Theirs is a full record but very long. |

What is useful in their format: each rule says **why**, names the **bug** that taught it, and names the **check** that
now guards it, with whether it was armed. "Things deliberately not here" lists what was left out and why, so nobody
re-adds it. Bug write-ups give symptom, cause and the family-wide shape.

What is noise: length (game-playground's `CLAUDE.md` is 192 KB). The same lessons are repeated across ten
repositories. Dated narrative accumulates ("Reviewed again ... run 10"). Counts in prose go stale (F25). File
instructions are addressed to an agent ("Read `../../CLAUDE.md` first"). For a maintainer, a short decision log plus a
bug log with "the test that guards it" would carry most of the value. `INFERRED`.

---

## 6. Bugs and lessons they found, and whether we share the risk

Full tables are in `notes/game-playground.md` §13 (P1-P22), `notes/game-simple-lobby.md` (L1-L12),
`notes/game-hungario.md` (H1-H7), `notes/dot-vote.md` (V1-V10), `notes/dot-server.md` and
`notes/dot-server-deploy.md` (D1-D6). Those where we share the risk:

| # | Their bug (evidence) | Lesson | Our exposure (file) |
|---|----------------------|--------|---------------------|
| B1 | Game switch never run end to end: six bugs, including a client mid-join never told and left in the old game (`dot-server/CLAUDE.md:419-466`) | **Run the switch in a test, including a player who is joining while it happens** | Plan step 2. Future `POST /api/room/session/new` with a `gameType` |
| B2 | Module never rebound after a game change; the room froze with everyone in it (`game-simple-lobby/CLAUDE.md:553`; `examples/dedicated.gd:919-931`) | the part that outlives the game must be told | Our frontend picks the screen from `GAMES[snapshot.gameType]` (`frontend/src/components/RoomScreen.tsx:171`), which helps. Per-game client state that is not in the snapshot would not reset |
| B3 | `reset_world` cleared a dictionary instead of destroying pieces; clients kept last round's pieces (`game-hungario/CLAUDE.md` "Bugs") | teardown must go through the notifying path | Same as B2 |
| B4 | Two lists went stale; a registered game could not be listed or voted for (`dot-server-deploy/CLAUDE.md:691-718`) | one list | `Program.cs:50-68` vs `frontend/src/games/registry.ts`: no test compares them (the frontend test `pollGames.test.tsx:134-136` checks only client keys) |
| B5 | Contract test passes when it cannot compare (`dot-server-deploy/examples/install_descriptor.gd:138-141`) | a check that cannot run must fail | Applies to any contract test we add |
| B6 | Vote clock advanced twice, so ran at double speed; one play recorded twice, halving cooldowns (`game-playground/CLAUDE.md:925-934`; `game-hungario/game/hungry_maps.gd:283-288`) | one owner of each clock and one signal that records a play | Next-game vote: use `ServerTimer` and record a play only where a session is created |
| B7 | A vote that could not open (too few players, cooldown) was never offered again (`dot-vote/CLAUDE.md` bugs) | latched triggers need retry | A room with too few players for any other game |
| B8 | "Extend" stayed on the ballot after extensions ran out (`dot-vote/CLAUDE.md`) | options reflect state when the ballot opens | "play again" or "same game" options |
| B9 | A nomination during an open ballot was accepted, then thrown away (`dot-vote/CLAUDE.md`) | refuse and say why | Proposals during a vote |
| B10 | Producer missing: an event consumed by two games and emitted by none (`game-playground/CLAUDE.md:176-191`) | pair every encoder with a decoder and test the pair | 3 SignalR events (`Services/RoomNotifier.cs:20-22`; `frontend/src/lib/useRoom.ts:45-47`). Small surface, but untested as a pair |
| B11 | Disconnect handler never ran (arity), so peers were never released (`game-playground/examples/dedicated.gd:770-781`) | drive the real disconnect | `PresenceTracker.Disconnected` is tested (`backend/RandomRoom.Tests/RoomServiceTests.cs:194-203`), `RoomHub.OnDisconnectedAsync` is not (no `RoomHub` in tests) |
| B12 | Test drove hooks directly; the real client never called them (`game-playground/CLAUDE.md` "The presentation layer") | drive the real producer | Engine tests call engines, not REST + SignalR (G above) |
| B13 | A guard that never fires looks like one never needed (`game-playground/CLAUDE.md:166-171`) | arm guards | No arming practice here |
| B14 | Same arithmetic in the check and the code, so both wrong (`game-hungario/CLAUDE.md` "Bugs") | a check must not copy the code's reasoning | Our setup limits are written twice by the same hand |
| B15 | Write before announce: entity owned by nobody; round announced before reset (`game-simple-lobby/CLAUDE.md` bugs; `game-hungario/CLAUDE.md`) | commit, then broadcast | We already do: endpoints publish after the service returns (`Endpoints/RoomEndpoints.cs:52-61`) and snapshots read untracked, fresh state (`GameSessionService.cs:154-157`) |
| B16 | Admin flag names that match nothing grant nothing, silently (`dot-server-deploy/cfg.example/groups.yml` comment; `dot-server/CLAUDE.md:691-708`) | refuse unknown names | Low; our roles are code |
| B17 | A non-numeric length became "permanent" (`game-simple-lobby/CLAUDE.md` bugs) | defaults that mean "forever" | Low; `Room__RetentionDays=0` means off and is documented (`README.md` "Data retention") |

Their bugs we do **not** share: those caused by prediction, physics, interest caching, clock lead, StringName sort
order, Godot UI anchors, pack mounting, and state persisted between test runs (we use throwaway databases).

---

## 7. What we can learn, ranked

Each idea gives: what it is, the evidence, what it touches, benefit, cost (S/M/L), risks, dependencies, and whether
the owner must decide.

**7.1 Contract tests for duplicated facts (owner's idea 2): CONFIRMED and refined.**
- Evidence: F10, F12, B4, B5. "A second count of anything is a second number that can disagree"
  (`game-playground/CLAUDE.md:917-920`).
- Our code: a backend test that lists every registered `IGameEngine` (`GameType`, `MinPlayers`, `MaxPlayers`) and
  writes them to a JSON fixture, plus a Vitest test that compares `GAME_LIST` (`frontend/src/games/registry.ts`) with
  it. Do the same for trivia caps and pack sizes (`DECISIONS.md` "Trivia limits", "Trivia packs";
  `frontend/src/games/trivia/setup.ts`). Better still, have the server serve the facts (`GET /api/games`) so the client
  stops keeping its own copy.
- Refinement from F11: the test must **fail** when the fixture is missing or stale, never skip. Test a different value
  at each end (their tick-rate trick).
- Benefit: removes the bug class that produced the accent-duplicate bugs "three times" (`NEXT_STEPS.md`). Cost: S.
  Risk: low. Depends on nothing. Owner decision: only for the `GET /api/games` endpoint.

**7.2 Bot-driven end-to-end tests (idea 7): CONFIRMED.**
- Evidence: F15, F16, B11, B12. game-simple-lobby's `sandbox` ("a real server and two real clients, over real
  sockets") is "the one that matters"; three of its bugs were found only there (`game-simple-lobby/CLAUDE.md:581-588`).
- Our code: a new `backend/RandomRoom.Tests/EndToEnd/` harness with `WebApplicationFactory`, real SignalR clients and
  per-game bot strategies (§5-G). It touches no production code.
- Benefit: catches seam bugs such as the End-versus-answer race (`NEXT_STEPS.md`). It is also the safety net for the
  game switch. Cost: M (S for the harness and 2-3 games, then about S per game). Risk: slow tests, so run a subset per
  PR and everything nightly. Owner decision: whether CI runs it on every PR.

**7.3 Switch games inside one room, plus a next-game vote (idea 1): CONFIRMED for the switch; vote CONFIRMED but much smaller.**
- Evidence: F3-F9, B1-B3, B6-B9. The ecosystem's working pattern is "the people layer outlives the game"
  (`game-hungario/CLAUDE.md:617-626`; `dot-server-deploy/examples/multigame.gd:16-35`).
- Our code: as in `docs/plan-switch-games-in-a-room.md` option A. `GameSession.GameType`; setup keyed by session
  (`Domain/RoomGameSetup.cs`, `RoomChoices`, `TriviaQuestions`, `GameStore.GetSetupAsync`); the
  `IGameEngine.ConfigureRoomAsync` signature; `GameSessionService.StartNewSessionAsync` (99-123); `RoomScreen.tsx:171`.
- Vote, taken from their lessons. Choices are the `GameType` keys, filtered by the room's player count (`MinPlayers`,
  `MaxPlayers`). The current game is excluded or kept per an owner decision (cooldown 1 play in
  `dot-server-deploy/cfg.example/vote.yml`). Plurality. Close when all have voted (`close_when_all_voted: true` in the
  same file). Ties go to the host or are broken in a fixed order, never in display order (`dot-vote/CLAUDE.md`
  "Presentation is not policy"). The result records how it was decided. The ballot is part of the snapshot, so late
  joiners see it (F22). One deadline owner (`ServerTimer`). Record a play only where a session is created (B6). Retry
  when a ballot cannot open (B7). There is no veto in dot-vote. A host override is our own design choice.
- Benefit: the owner's stated need. Cost: L for the switch (data model, 19 engines), M for the vote. Risks: B1-B3, and
  the setup migration on live data (plan "Risks"). Depends on 7.1 and 7.2. Owner decision: yes (§10).

**7.4 Add a player to a room later (idea 8): CONFIRMED as a prerequisite, indirectly.**
- Evidence: every ballot filters by player count (`dot-vote/addons/dot_vote/core/dot_vote_choice.gd:127-140`;
  `game-hungario/game/hungry_maps.gd:111-115`). A filter that leaves only a few games makes the vote pointless. The
  ecosystem itself never needs this, because players join servers freely.
- Our code: host-only `POST /api/room/players` creating a `RoomPlayer` with an invite (`Services/RoomAdminService.cs`),
  between games only (as seat reset is, line 165), with an audit row.
- Benefit: unlocks 4+ player games. Fixes "forgot a friend". Cost: S-M. Risk: max 12 seats. Owner decision: yes
  (plan question 3).

**7.5 Tonight's scoreboard (idea 4): PARTLY SUPPORTED.**
- Evidence: the ecosystem **never** combines scores across games. Boards are scoped by mode because "a top mass in
  Frenzy and a top mass in Classic are not the same number" (`game-hungario/CLAUDE.md` "Boards"). Scoreboards are keyed
  by a stable player key "never a peer id" (`dot-match/README.md:64-70`).
- So: store each session's **placings**, not points, keyed by seat, and show a room board that combines placings.
  Our code: a new `SessionResult` table written in `GameSessionService.Complete`'s callers (66-70, 94-95), plus a
  shared ranking rule. The frontend `Scoreboard` already has one ("ties share a rank", `DECISIONS.md`).
- Cost: M. Risk: the scoring rule for games with no winner (Random Picker, story games). Owner decision: yes, on how to
  count games without placings.

**7.6 Hand the host role to another player (idea 6): REFINED; the evidence is about something else.**
- Their "host migration" moves the **authority process** in P2P sessions. Our server is always the authority, so for
  us this is purely a permission change. Their reasoning still transfers in two ways. Decide by what is lost (for us,
  nothing in game state; it is like the lobby's "nothing is built"). And use a rule every party computes from facts the
  server already stores, never from measurements that parties see differently (`dot-peer-to-peer/README.md:18-26`).
- Our code: `Room.HostPlayer`; a host-only "make X host" action between games, with an audit row. The recovery code
  must be re-issued to the new host and the old one revoked (`DECISIONS.md` "Host recovery code"). Automatic hand-over
  when the host is offline is a separate, larger decision.
- Cost: S (manual) to M (automatic). Owner decision: yes.

**7.7 Spectating (idea 5): WEAKLY SUPPORTED; I did not read their spectate addon.**
- Evidence: spectating exists (`pg_spec` in `game-playground/game/playground_module.gd:282-285`; game-simple-lobby
  "Watching somebody else", `CLAUDE.md:745-767`), but as a camera feature. dot-spectate is NOT READ. dot-vote supports
  `is_spectator_fn` so spectators do not count as voters (`dot-vote/CLAUDE.md` table).
- What transfers: spectators must not count toward votes or player minimums. We already have the safe viewer-less view
  (`IGameEngine.cs:49-55`, `GameSessionService.cs:19-21`).
- Cost: M. Risk: a spectator link is a shared secret. Owner decision: yes.

**7.8 Reuse a past setup (idea 3): NOT SUPPORTED by this evidence.**
- Nothing in the ecosystem saves a play's setup for reuse. Their setup is server configuration that every play reuses
  automatically. "Saving a build" is listed as deliberately absent (`game-playground/CLAUDE.md` "Things deliberately not
  here"). The idea may still be good for us; the evidence neither helps nor hurts it. One related point: switching back
  to a game should copy the previous session's setup, which the plan already says.

**New ideas from the evidence:**
- **N1. Client/server version check.** The server sends a build id in each snapshot; a client with a different one
  shows "a new version is available, refresh". Evidence: `dot-net/CLAUDE.md:306-321`. Touches `RoomSnapshot`
  (`Services/RoomSnapshot.cs`) and `useRoom`. Cost S. Owner decision: no.
- **N2. Test the switch with a joining player** (B1). Part of 7.3.
- **N3. A host "status" view** that says what the room is doing (game, phase, deadline, who is online). Their
  `pg_status` answers "what is this server doing" in one place. Cost S. Owner decision: no.
- **N4. Fix our stale doc comment.** `Services/RoomSnapshot.cs:6` says "The same view is sent to every participant";
  per-viewer snapshots replaced that. Cost XS.

---

## 8. What does not transfer, and why

- **Tick loops, client prediction, reconciliation, interpolation, lag compensation, three timelines**
  (`dot-net/CLAUDE.md:15-38`). Our games are turn and phase based. Lazy server deadlines are enough.
- **Acked baselines and delta snapshots.** We send whole per-viewer snapshots over a reliable channel. The one
  bandwidth concern (Sketch Guess strokes, `NEXT_STEPS.md`) is a delta question, not an ack question.
- **Interest management by distance.** Our "interest" is hidden information, which we already handle per viewer.
- **P2P hosting, WebRTC, relays, join-code rendezvous.** We always have a server.
- **Signed content packs, mount paths, `class_name` rules.** We do not deliver executable content.
- **The full 80-setting vote engine, flags plus immunity, RCON, ban sharing.** Built for public servers run for
  strangers. Friends' rooms with one host need a handful of rules.
- **Physics, maps, zones, movement tunables, NPCs, vehicles.** Real-time 3D.

---

## 9. Proposed way forward

**The decision first: "the room is locked to one game today. Do we change the data model, or link rooms together?"**

**Recommendation: change the data model (option A), in a release with no behaviour change first.** Reasons from the
evidence:

1. Every system in the ecosystem that switches games keeps the **people layer** and swaps only the **game layer**.
   game-hungario rebinds instead of rebuilding because rebuilding "is a disconnect for everybody"
   (`game-hungario/CLAUDE.md:617-626`). dot-server-deploy keeps the same module across modes "because reloading would
   drop every connected player" (`examples/multigame.gd:33-34`). Linked rooms are the "rebuild" path: new URL, new
   page, new sockets, and a scoreboard spread across a chain.
2. Our room already is the people layer (players, PINs, host, audit, retention). Our session is already the play.
   Option A only moves setup and game type to where the play is. That is the same shape as
   `dot-server/CLAUDE.md:476-481` ("it changes the scene and tells whatever modules are already loaded").
3. Their bugs show where it breaks (B1-B3). The risk is in **untested switching**, not in the data model, and our
   test practice can cover it before users see it.

Linked rooms remain a fallback if step 3 below turns out too large.

**Roadmap** (each step ships on its own):

| # | Step | Size | Risk | What to test | Owner decides first |
|---|------|------|------|--------------|---------------------|
| 1 | Contract tests: game keys, player ranges, trivia caps and pack sizes, server vs client; fail when unable to compare (7.1). Fix the `RoomSnapshot.cs` comment (N4) | S | Low | Change one copy, see red; delete the fixture, see red | Whether to also add `GET /api/games` |
| 2 | Bot end-to-end harness over real REST + SignalR for 3 games, then all 19 (7.2) | M | Low (tests only) | Whole game to Completed; no secrets in other bots' pushed snapshots; arm once | Run on every PR or nightly |
| 3 | Data model only: `GameSession.GameType`, setup keyed by session, backfill, engines read setup by session; no behaviour change (plan "Order of work" 1) | M-L | High (migration) | Existing per-game tests plus step 2 unchanged; migration against a copy of real data; reversible | Go-ahead for option A |
| 4 | Host "Change game" between games, filtered by player count, audited; tests include a player joining during the switch and secrets not carried over (7.3, B1-B3) | M | Medium | Switch A to B to A with bots online; a player mid-join; Word Spies key absent after switch | Host-only or players can propose |
| 5 | Add a player to a room between games (7.4) | S-M | Low | Seat cap, invite claim, audit, cannot add mid-game | Wanted at all? |
| 6 | Version check in snapshots (N1) | S | Low | Old client shown "refresh" | — |
| 7 | Tonight's scoreboard by placings (7.5) | M | Medium | Ties, games with no winner, sessions ended early | Scoring rule; reset scope |
| 8 | Next-game vote, minimal rules (7.3) | M | Medium | Ties, all voted, too few players then retry, late joiner sees ballot, one play recorded once | Host veto/override; can a game repeat |
| 9 | Host hand-over, manual (7.6) | S | Medium (recovery code) | Code moves; old host's code revoked; audit | Rules for automatic hand-over |
| 10 | Spectator link (7.7) | M | Medium (secret link) | Viewer-less payload only; not counted in votes or minimums | Wanted at all? |

---

## 10. Open questions for the owner (most blocking first)

1. **Go-ahead for option A** (session owns game type and setup), knowing step 3 is a data migration on live data?
   Or is linked rooms (B) acceptable instead? Blocks steps 3, 4, 7 and 8.
2. **Should the bot end-to-end suite run on every pull request, or nightly?** Blocks step 2's CI wiring.
3. **Who chooses the next game:** the host only (step 4), or players by vote (step 8)? If by vote, may the host
   override the result, and may the same game be played twice in a row?
4. **Can the host add players mid-evening** (between games)? Without it, many games cannot be offered to small rooms.
5. **Tonight's scoreboard:** per room for ever, or per evening? How do games without a winner (Random Picker, the
   story games) count?
6. **Should the server publish the game list and limits** (`GET /api/games`), so the frontend stops keeping its own
   copy?
7. **Host hand-over:** manual only, or automatic when the host is offline for N minutes? What happens to the recovery
   code?
8. **Spectators:** wanted? If so, can anyone holding the link watch, or only people the host lets in?

---

## 11. Unexpected findings

- **F1. Built at speed by an AI assistant and little played.** Every repository's README says so (e.g.
  `game-playground/README.md`, "From Maintainer & WARNING"). Commits all date from late September or early October
  2026 (`progress.md` inventory). Their documents are written for an agent: each starts "Read `../../CLAUDE.md` first".
  Weigh the lessons as well-argued engineering notes, not as field experience.
- **F11. Their own contract test passes when it cannot compare.** `dot-server-deploy/examples/install_descriptor.gd:138-141`
  records two passing checks when the sibling repository is missing. That contradicts dot-vote's rule that integration
  sections "fail loudly rather than being skipped". dot-ci also lets a repository with no detectable suite pass
  (`dot-ci/README.md:79-81`). That is deliberate, but it is how game-simple-lobby's two most important suites never
  ran in CI (`game-simple-lobby/.github/workflows/ci.yml` comment). By reading the files as text, I confirmed that
  the four compared fields currently agree for five games (see `notes/dot-server-deploy.md`).
- **F25. Test counts drift between README, CLAUDE.md and code.** game-playground: README says 432 / 271 / 217 checks,
  `CLAUDE.md:27` says 359, `CLAUDE.md` "Validating" and the code say 563 / 294 / 224
  (`examples/headless_playground.gd:52`, `headless_net.gd:58`, `dedicated.gd:49`). game-simple-lobby's README and
  `CLAUDE.md` disagree on every suite. A project whose central rule is "one copy of a number" keeps three copies of
  its own test counts.
- **F26. game-playground and dot-vote disagree on how many settings dot-vote has.** `game/playground_vote.gd:10,99`
  says "fifty-five settings". dot-vote's own documents say 80 (`dot-vote/CLAUDE.md` table, `README.md`). This is minor, but
  it is the same drift.
- **F27. The repositories move daily.** dot-ci's head changed from `8a41ce6` (preflight) to `d26b5a4` during this
  session. Five repositories had commits on the day I read them. Any line reference here may go stale quickly. Commit
  hashes are in §3.
- **F28. The deployment once leaked a generated RCON password into a public repository**, which led to
  `cfg.example/` (tracked) versus `cfg/` (generated, ignored) (`dot-server-deploy/CLAUDE.md`, "`cfg/` is written by
  setup"). Our equivalent, `.env` git-ignored with `.env.example` tracked (`README.md` "Run locally"), already follows
  that rule.
- **F29. The vote engine has no veto**, despite the brief asking about one. An admin overrides with `setnextmap` or
  `endvote` (`dot-vote/README.md` commands; grep for "veto" in `dot-vote/addons/` finds nothing).
- **F30. Our own doc drift.** `backend/RandomRoom.Api/Services/RoomSnapshot.cs:6` still says one view goes to every
  participant. The code sends one view per player (`Services/RoomNotifier.cs:8,51-57`).

---

## 12. Appendices

### Appendix A: evidence index

| Finding | Repository | File | Lines / heading |
|---------|-----------|------|-----------------|
| F1 | game-playground | README.md | "From Maintainer & WARNING" |
| F2 maps not games | game-playground | game/playground_vote.gd | 13-17 |
| F3 server switches games | dot-server | CLAUDE.md | 419-424 "Game switching"; 90-91 |
| F4 game vote on a multi-game server | dot-server-deploy | CLAUDE.md; cfg.example/vote.yml | "The players choose the next game" |
| F5 people outlive the game | game-hungario; game-simple-lobby; dot-server-deploy | CLAUDE.md; game/room_module.gd; examples/multigame.gd | 617-626; 296-356; 16-35 |
| F6 switch bugs | dot-server; game-simple-lobby | CLAUDE.md | 425-474; 553 |
| F7 ballot from registry, min players | game-hungario | game/hungry_maps.gd | 111-140, 277-281 |
| F8 lobby excluded, per-game limits in game.yml | dot-server-deploy | cfg.example/vote.yml; CLAUDE.md | `vote_exclude`; vote section |
| F9 dot-vote model, no veto | dot-vote | CLAUDE.md; core/dot_vote_rules.gd; core/dot_vote_choice.gd | "The one design decision"; 35-160; 7-29, 127-140 |
| F10 descriptor and its copy | game-playground | game.yml | 13-15 |
| F11 vacuous pass | dot-server-deploy | examples/install_descriptor.gd | 116-160 (138-141) |
| F12 one source of truth | game-playground | CLAUDE.md; game/playground_config.gd; examples/dedicated.gd | 758-778, 845-849, 917-923, 946, 991-993; 7-12; 464 |
| F13 declared totals | game-playground | examples/headless_playground.gd; examples/dedicated.gd | 52-58, 128-152; 36-49 |
| F14 arming | game-playground; dot-vote | CLAUDE.md | 46, 808; "Every new check was armed" |
| F15 bots and real sockets | game-playground; game-simple-lobby | examples/headless_playground.gd; CLAUDE.md | 20-46; 581-588 |
| F16 bugs in seams | game-playground | CLAUDE.md | 13-24 |
| F17 host migration per game | game-* | game/*_party.gd | playground 14-20, 63; lobby 66; hungario 56; arena 61; g2gfast 63 |
| F18 flags + immunity | dot-server; dot-server-deploy | CLAUDE.md; cfg.example/groups.yml | 190-206; header |
| F19 per-viewer snapshots | ours | Games/IGameEngine.cs; Services/RoomNotifier.cs | 49-55; 51-57 |
| F20 test isolation | ours | README.md | "Test" |
| F21 two copies of game facts | ours | DECISIONS.md; NEXT_STEPS.md | "Trivia limits", "Trivia packs"; "Setup rules are written twice" |
| F22 late joiner re-sent vote state | dot-server-deploy | CLAUDE.md | "Four things about the wiring in TmcVote" |
| F23 READY gate | game-playground | CLAUDE.md | 1687-1695 |
| F24 state between runs | game-playground; game-hungario | CLAUDE.md | 728-733; "A suite that fails on its ninth run" |
| F25 count drift | game-playground; game-simple-lobby | README.md; CLAUDE.md; examples | "Validating"; 27; constants |
| F26 setting count | game-playground; dot-vote | game/playground_vote.gd; CLAUDE.md | 10; table |
| F27 moving repositories | dot-ci | (git) | 8a41ce6 to d26b5a4 |
| F28 leaked RCON password | dot-server-deploy | CLAUDE.md | "`cfg/` is written by setup" |
| F29 no veto | dot-vote | addons/ | grep "veto": no match |
| F30 our stale comment | ours | Services/RoomSnapshot.cs | 6 |

### Appendix B: suspicious instructions found

| Text | File | What I did |
|------|------|------------|
| "Read the family-wide conventions in `../../CLAUDE.md` first" (and the same in each repository) | game-playground `CLAUDE.md:9`; game-simple-lobby `CLAUDE.md:3-4`; dot-game `CLAUDE.md:5`; dot-vote `CLAUDE.md:3` | Not followed. The file is outside the repositories. Recorded as NOT READ. |
| Instructions to run commands (`godot --headless ...`, `tools/check.sh`, symlink loops, `./server install-games`) | many READMEs and CLAUDE.md "Validating" sections | Not run (rule 7). |

None asked for secrets, settings changes or data transfer, or told me to ignore my instructions.

### Appendix C: assumptions

- Scratch clones went in the session's scratch directory, not `/tmp/game-research`. It is outside the repository.
- Where a README and the code disagree on a count, the code's `const CHECKS` is taken as current.
- "game-hungario votes over games" means its six modes are registered with dot-server as six separate games
  (`game/hungry_module.gd:1408-1432`). They are modes of one game, not different games.
- dot-vote's behaviour was taken from its README and CLAUDE.md where I did not read the code (noted in its notes file).
- The times in `progress.md` come from the container clock, which I could not check.

### Appendix D: glossary

| Term | Meaning |
|------|---------|
| addon / `dot-*` | a Godot addon in its own repository, installed under `addons/<name>/` |
| `game.yml` | a game's descriptor: name, scenes, module, players, cvars, metadata |
| `DotGameDescriptor` | dot-server's in-memory form of a descriptor |
| module (`DotModule`) | server-side code loaded into dot-server; registers commands, cvars and hooks; outlives a game change |
| cvar | console variable: a named setting changed from the console, a config file or RCON |
| `DotConfig` | dot-core's layered config class: defaults < file < environment < command line |
| `server.cfg` / `autoexec.cfg` | console scripts run before / after the listener opens |
| flags / immunity | permission strings (`changemap`, `generic`, ...) and a level that decides who may act on whom |
| RCON | remote console over the network |
| pack / `.pck` | a Godot resource pack: scenes, scripts and assets mounted at run time; cannot be unmounted |
| content origin | the web server packs are fetched from |
| `requires.json` | a pack's list of addon API levels it needs |
| bridge | a game's glue between its world and the netcode (`*_net_bridge.gd`) |
| snapshot | the server's periodic state message to a client |
| READY | a client's message saying it has built its scene and can receive |
| interest | which entities a client is told about |
| audience OWNER | a replicated field sent only to its owner |
| prediction / reconcile | a client simulating its own input ahead, then correcting to the server |
| changelevel / changegame | the operator command that switches the running game |
| rotation | the default order of what plays next |
| RTV | "rock the vote": players asking to end the current map or game early |
| nomination | a player proposing a choice for the next ballot |
| extend | a ballot option to keep the current map or game for longer |
| P2P party | a player-hosted session through dot-peer-to-peer |
| host migration | electing a new host when the host leaves a P2P session |
| armed (a check) | broken on purpose once to prove the test catches it |
| CHECKS / SECTIONS | declared totals a suite compares with what actually ran |
| TMC / backbone | the maintainer's platform and its closed-source web service |
