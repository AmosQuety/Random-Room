# dot-server

- URL: https://github.com/modcommunity/dot-server. Commit `154bc60`, 2026-10-02. Licence: MIT. 1.1 MB, 85 files,
  ~19,700 lines (largest: `server/dot_server.gd` 2,510, `examples/dedicated_server.gd` 2,268, `console/dot_console.gd`
  1,284).
- Read: `CLAUDE.md` headings and lines 22-92, 157-244, 332-359, 419-505, 652-709, 804-820;
  `game/dot_game_descriptor.gd` (field list, `validate()` 94-143); `admin/dot_admin_flags.gd` (constants);
  `admin/dot_admin_manager.gd` (grep for immunity).
- NOT READ: the server, console, RCON, chat, ban, envelope code bodies; the three example suites; CLAUDE.md sections
  on RPC envelope, notices, ban sharing, address limits, queries.

## Purpose

"The piece a server owner actually runs ... shaped after twenty years of dedicated-server" practice (`README.md` first
line). It gets a player "from typed an address to in the world and hands over"; state replication is deliberately a
game's concern (`CLAUDE.md:812-816`).

## Game descriptor and switching (A1, A2, B4, B5)

`DotGameDescriptor` fields (`game/dot_game_descriptor.gd:15-87`): `game_id`, `display_name`, `version`,
`manifest_url`, `content_id`, `content_groups`, `dependencies`, `scene`, `client_scene`, `cvars`, `max_players`,
`min_players`, `metadata`. `validate()` (94-143): `game_id` required, a name or `<owner>/<name>`, each half a slug;
must name a scene; scene path must be safe-relative; dependencies must be `<owner>/<name>@<version>`.
This is the in-memory form of `game.yml` (reader of `game.yml` itself is in dot-server-deploy's installer; see that
note).

`DotGameManager.change_game`: fire a cancellable `game_changing` event -> tell clients to fetch new content -> wait for
all (or timeout) -> free old scene -> instantiate new -> put everyone back through `LOADING` (`CLAUDE.md:419-424`).
Clients still downloading at the timeout are kicked; a failed change restores the previous game; old content is
released (`CLAUDE.md:487-495`). `SPAWNED -> DOWNLOADING` is a legal session transition: "that is the game change, and
it is the whole reason the family exists" (`CLAUDE.md:90-91`).

**dot-server does not load a module for a game**: it changes the scene and tells already-loaded modules
(`_module_game_changed`), "deliberately, because a game with no server-side behaviour is legitimate"
(`CLAUDE.md:476-481`). So modules (people, chat, moderation) outlive games; worlds do not.

Six bugs found when the switch was first actually run end to end (`examples/content_switch.tscn`), all of them
"parse-clean" (`CLAUDE.md:425-466`): server never fetched its own content; clients kicked for reporting the new key;
`LOADING -> DOWNLOADING` illegal so a just-joined client was never told and stayed in the old game; stale content key
made a client look ready; first client told to load the old game; progress subscription duplicated each change. Also
`swap_when_all_ready` tested below an unconditional return, and descriptor `cvars` read by nothing
(`CLAUDE.md:468-474`).

**Lesson for us:** every part of "switch the game with people in the room" that had never been run was broken. A
mid-join client (our analogue: a player whose page is loading during a switch) was the case missed.

## Config layering and boot order (F13)

`config resource -> JSON file -> env -> argv`, then console and cvars, then `server.cfg` (startup-only cvars still
settable), then subsystems, then the listener opens (startup-only cvars lock), then `autoexec.cfg`, then `+command`
arguments (`CLAUDE.md:52-69`). Startup-only cvars (port, tickrate, transport) are refused at runtime because they
"would half-apply" (`CLAUDE.md:24-27`).

Console design (`CLAUDE.md:157-188`): values are strings; flags CHEAT, PROTECTED (never printed, redacted in the log),
STARTUP_ONLY, ARCHIVE, NEEDS_PERMISSION; out-of-range values are **clamped with a log line**, not refused; every path
in (terminal, RCON, chat, config file, module) goes through one `DotConsole.execute` with a `DotCmdContext`, so one
permission check covers all.

Invalid files: "A malformed `admins.json` is loud and keeps the previous state. Silently meaning 'no admins' turns a
typo into an unmoderated server. Same for `bans.json`" (`CLAUDE.md:207-209`).

## Permissions (C7)

"Flags, not roles, because operators do not agree on what a 'moderator' is. Strings, not bits" (`CLAUDE.md:190-194`).
Flags: `root`, `reservation`, `generic`, `kick`, `ban`, `unban`, `mute`, `changemap`, `cvar`, `cheats`, `slay`,
`teleport`, `config`, `rcon`, `chat`, `vote`, `password`, `modules`, `logs`, `admin` (`admin/dot_admin_flags.gd:30-94`).
Unknown flags are reported, not refused (games add their own). **Immunity** is separate: "may kick" and "may be kicked"
are different questions; equal immunity cannot act on equal (`CLAUDE.md:200-205`). Sources (file, site groups, auth)
merge as a union of flags and the higher immunity (`CLAUDE.md:204-206`; `dot_admin_manager.gd:113-174`).
`run_command_as_uid` runs a relayed command (website, Discord bridge) with **that person's** permissions, not root
(`CLAUDE.md:680-689`). `admin_add <uid> moderator` used to create an admin with a flag named "moderator" that granted
nothing (`CLAUDE.md:691-708`).

## Naming a player (C9)

Target resolution order: `#userid`, userid, `@me`, `ip:` (everybody at the address, on purpose), exact display name,
username, account id, substring last. "Every exact form is matched before the substring one" (`CLAUDE.md:332-352`).
`banip` checks immunity against every session at the address, otherwise "a junior admin removes a senior one by naming
their housemate" (`CLAUDE.md:353-356`).

## Security decisions (D10, F13)

Empty RCON password = no listener; constant-time password compares; RCON lockout and allow-list checked before the
password; `exec` refuses path traversal; authenticated players cannot override display name (only guests may name
themselves, "Otherwise any account can appear as 'Administrator'"); chat sanitised (control, zero-width, bidi
characters); a client refuses absolute scene paths from a server; admin chat filtered on the server ("Asking clients to
hide messages they are not entitled to see is not a control") (`CLAUDE.md:212-244`).

## Testing

`dedicated_server` 321 checks; `signon_revision` 68; `content_switch` 46 "the last of which compares the total"
(`CLAUDE.md:718-729`).

## What transfers

- Modules (people) outlive games (worlds); a game change is a state transition of every session, with a legal path
  from "still loading" too.
- One entry point for every command source with one permission check.
- Flags plus immunity, rather than roles; refuse a malformed admin file and keep the previous state.
- A relayed action runs with the relaying person's permissions.
- Server-side filtering of what a player is not entitled to see (we already do this: per-viewer payloads).
