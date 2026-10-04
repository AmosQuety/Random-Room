# dot-peer-to-peer

- URL: https://github.com/modcommunity/dot-peer-to-peer. Commit `37d47c1`, 2026-09-24. Licence: MIT. 196 KB, 27 files.
- Read: `README.md` 12-52; `CLAUDE.md` 23-78. NOT READ: code, suites.

## Purpose

Sessions players host for each other: join codes, readiness, deterministic host migration, WebRTC transport reached by
string (`README.md` first lines, "The transport is never named").

## Host election and migration (C8)

- "stable first, then longest here, then lowest id"; **every peer computes it from the member list, nobody is asked**,
  because "the moment a host disappears is precisely the moment messages are not arriving" (`README.md:18-26`;
  `CLAUDE.md:23-31`). Latency is not used: "a value two peers disagree about is a value that can elect two hosts".
- A host **claim** is checked against the election before it is believed (`CLAUDE.md:36`).
- Whether a game migrates at all is the game's choice (`migrate_host`); see the five-game table in
  `game-playground/game/playground_party.gd:14-20`.
- Trust levels: host-authoritative, verified, sandboxed ("nothing leaves the session: no records, no statistics, no
  leaderboard"); "A host who can cheat and a leaderboard are not two features. They are one exploit."
  (`README.md:28-34`).

## Join codes

Alphabet `23456789ABCDEFGHJKLMNPQRSTUVWXYZ` (no 0/O/1/I/l); six characters ≈ a billion codes; checked for shape before
any lookup, "not a way to find out which sessions exist" (`README.md:47-52`; `CLAUDE.md:69-73`). Our recovery code
also avoids look-alike letters (`DECISIONS.md` "Host recovery code"). EQUIVALENT.

## Bugs (`CLAUDE.md:38-45`)

Announced before it could hear the reply ("Bind first, then announce"); host/join not awaited, so the real HTTP
signaller had never worked because every test used a synchronous loopback; `add_member` silently made the first member
host, so the answer depended on message order.

## Transfer to "hand the host role to another player"

A deterministic rule (for example: longest-seated online player) that every client and the server compute the same
way, with the server as the only authority, is simpler for us than for them because we have a server. The relevant
lesson is the rule's inputs: use facts the server already stores (`RoomPlayer.ClaimedAt`, `CreatedAt`), never
something measured differently by each party. `INFERRED`.
