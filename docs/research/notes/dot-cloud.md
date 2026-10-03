# dot-cloud

- URL: https://github.com/modcommunity/dot-cloud. Commit `c88670e`, 2026-09-30. Licence: MIT. 676 KB, 67 files.
- Read: `CLAUDE.md` headings, 17-40, 121-160, 226-245. NOT READ: code, sources, publishing sections, README body.

## Purpose (A3)

Runtime content delivery: a game gets its content to a player at runtime instead of baking it into the build
(`README.md` first line).

## Pack format and what it allows and forbids

- A Godot resource pack "can never be unmounted"; mounting game B over game A's paths gives "a blend of two content
  sets" with no error. So **content mounts at `res://<mount_root>/<content_id>/<version>/`**, version in the path, and a
  path is never reused (`CLAUDE.md:17-37`).
- "The manifest is the entire trust boundary": packs can contain scripts, so a client mounting an unsigned manifest
  "runs whatever the server sent". `require_signed_manifests` defaults to true and boot is refused without a trusted
  key (`CLAUDE.md:121-133`). Signatures live outside the signed document (`DotCloudEnvelope`, RS256, `key_id`)
  (`CLAUDE.md:154-160`).
- A manifest `version: "../.."` once validated and mounted at `res://`; versions are now checked as a single safe path
  component (`CLAUDE.md:135-146`).
- Objects are **content-addressed** by SHA-256; nothing unverified is ever written to the object directory; the index
  is disposable (`CLAUDE.md:226-245`).
- A pack's `requires.json` lists the addon API levels it needs; a pack needing a newer API is refused before it mounts,
  with a sentence saying whether the server or the client must update (`CLAUDE.md:148-152`).
- Why scripts are named by path: a mounted pack's `class_name` globals are not registered in the host, so delivered
  games reference their own files by path (`dot-server-deploy/CLAUDE.md:20-34`;
  `game-playground/CLAUDE.md:292-299`).

## Compare

NOT COMPARABLE for code: our 19 games are compiled into one server and one frontend bundle (`Program.cs:50-68`,
`frontend/src/games/registry.ts`), and each frontend game is a lazy chunk (`DECISIONS.md` "`GameModule` extended
additively"). Content (trivia packs, prompt banks) is embedded, versioned JSON (`Games/Shared/ContentBank.cs`), which
is the safe end of their spectrum: nothing executable is ever fetched at run time. If we ever let hosts upload content
packs, their two rules transfer: content-address and verify, and never let uploaded content carry code.
