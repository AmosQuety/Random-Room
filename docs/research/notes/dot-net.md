# dot-net

- URL: https://github.com/modcommunity/dot-net. Commit `b26b6d0`, 2026-10-03. Licence: MIT. 720 KB, 67 files.
- Read: `CLAUDE.md` headings, lines 15-38 (timelines), 75-97 (security), 280-378 (interest cache, two builds on one
  wire, wire format, funnel, acked baselines). NOT READ: code, the 140-line "Bugs the demo caught" section, rest.

## Purpose

"What makes a game multiplayer" (`README.md`): snapshots, input prediction, interpolation, interest management,
lag compensation.

## Sync model (D10)

- Three timelines on a client: input ahead, server estimated, render behind; drift corrected by scaling tick length,
  snapping only past a duration threshold (`CLAUDE.md:15-33`).
- **Clients send inputs, never state**; inputs sanitised on the server after decode; message direction enforced on
  receipt "against the transport's view of the sender, never a peer id inside the payload"; spawns name a prefab id,
  never a scene path; net ids never reused (`CLAUDE.md:75-89`).
- Hidden information: `DotNetVar.Audience.OWNER` keeps owner-only values (ammo, cooldowns) off other players' wire;
  "Interest management is the anti-cheat that works. Data never sent cannot be drawn on a wallhack" (`CLAUDE.md:90-93`).
- **Acked baselines**: per peer, `acked`, `pending`, `believed`; a property is re-sent only when a snapshot carrying it
  is confirmed lost; "no retransmit queue, no timers, and nothing extra on the wire" (`CLAUDE.md:350-366`). A client
  acks only when it applied the whole packet (`CLAUDE.md:374-378`).
- Interest cache bug: an entity spawned since the cache was built reached nobody; owned and always-relevant entities
  are now pinned into every cached answer (`CLAUDE.md:280-304`).
- **Versioned wire**: message ids are derived from the type name (SHA-256 prefix); every body is length-framed so an
  unknown or newer message is skipped; schemas exchanged so a missing *required* type refuses the join with "This
  server's game needs a newer game client."; rule: "append fields, never reorder, retype, re-width or remove one"
  (`CLAUDE.md:306-321`).

## Compare with ours

We send full per-viewer snapshots over a reliable channel (SignalR over WebSocket), so acks and baselines are not
needed; our sequence number plays the "newer state wins" role (`backend/RandomRoom.Api/Services/RoomSnapshotSequencer.cs`;
`frontend/src/lib/snapshots.ts`). Their owner-only audience and "never sent" rule is the same principle as our
`GetPayloadForAsync(viewer)` (`backend/RandomRoom.Api/Games/IGameEngine.cs:49-55`). What transfers: the **versioning
rule for payloads** (append-only fields, a client told plainly to refresh when it is too old). We have the "unknown game
type ... Try refreshing the page" message (`frontend/src/components/RoomScreen.tsx:184`) but no version check between a
cached client bundle and a newer server. `INFERRED` risk after a deploy that changes a payload shape.
