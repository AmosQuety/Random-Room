# dot-moderation

- URL: https://github.com/modcommunity/dot-moderation. Commit `e869fad`, 2026-09-24. Licence: MIT. 444 KB, 45 files.
- Read: `README.md` 14-52, 131-138. NOT READ: code, `CLAUDE.md`, mod-tools and storage sections in detail.

## Model (C9)

- Bans, kicks, gags, voice mutes and warnings are **one durable record** (`DotPunishment`: kind, subject, reason,
  issuer, issued, expires) so "a muted player reconnects and can talk again" stops being true (`README.md:14-36`).
- The subject is an **opaque durable key, never a peer id**, normalised (`uid:backbone:abc`, `ip:203.0.113.9`, port
  stripped) so a ban and a check use the same spelling (`README.md:33-38`).
- Expiry is answered on read, never swept on a timer; a revoke keeps the record ("The history is the point")
  (`README.md:34-35`).
- Account bans and address bans both exist because a guest "comes back as a different guest" (`README.md:41-45`).
- The record enforces nothing on its own; dot-server asks `dot_ban_source.check_admission(uid, address)` on every join
  (`README.md:47-51`). Appeals are "a website" (`README.md:138`).

## Compare

Our moderation is the host's seat reset (between games only), host recovery, delete room, and the append-only
`RoomAuditEvents` (`DECISIONS.md` "PIN recovery", "Host recovery code"). There is no kick/ban: a reset frees the seat and
the host can then not re-invite (`INFERRED`: a reset returns a fresh invite link to the host, so removing someone means
resetting and not sending the link). For a friends' room this is proportionate; their durable-record model exists for
public servers with strangers. NOT COMPARABLE in scale; their "revoke keeps the record" and "expiry on read" are
consistent with our append-only audit.
