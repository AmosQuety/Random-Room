# Repositories skimmed (README first paragraph only) or not read

Purpose lines come from the first paragraph of each README (fetched by `git clone --depth 1`). Licence is the first line
of `LICENSE` (all "MIT License"). These did not bear on the owner's questions beyond what is noted.

| Repository | Purpose (README) | Why not read deeper |
|------------|------------------|---------------------|
| dot-chat | text chat with server-side moderation | covered through game-playground's chat sections |
| dot-inventory | inventory as a document a server can check | covered through game-playground "The bag over the wire" |
| dot-timer | anything run against the clock; careful about what decides a leaderboard | real-time timing; covered through game-playground |
| dot-combat | health, damage, hit resolution | real-time 3D |
| dot-loadout | what a player may bring into a match, checked on the server | real-time 3D |
| dot-npc, dot-npc-ai | NPCs and their decisions | real-time 3D |
| dot-player-controller | movement (25 MB) | real-time 3D |
| dot-props | spawnable props, physics gun | real-time 3D |
| dot-ui | screens and menus, no art | engine UI |
| dot-user-avatar | cosmetics a server can check | out of scope |
| dot-vehicle, dot-physics, dot-spawn, dot-player, dot-player-char, dot-player-class, dot-team | gameplay layers enabled in `game-playground/project.godot` | real-time 3D; dot-team noted via game-simple-lobby |
| dot-platform, dot-auth | identity hub; sign-in | identity covered through dot-user and game-playground |
| game-arena | demo deathmatch; `migrate_host = false` (`game/arena_party.gd:61`) | only the migration setting checked |
| game-g2gfast | demo surf/bhop timer game (76 MB); `migrate_host = false` (`game/g2g_party.gd:63`) | only the migration setting checked |
| zee-dot-weapons | a weapons pack built on the family | out of scope |

Found to exist with `git ls-remote` but not cloned or read: dot-2d, dot-objective, dot-bootstrap, dot-voice, dot-audio,
dot-settings, dot-weapon, dot-browser, dot-spectate, dot-2d-hungry, dot-fx (modcommunity); mg-buses-from-hell and mg-smash-copter (gamemann). NOT READ.

Named in the notes but not found under either owner: dot-query, game-blob, website-city, game-dev, dot-randomness,
dot-p2p. UNREACHABLE or private; I cannot tell which.
