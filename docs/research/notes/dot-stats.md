# dot-stats, dot-leaderboard, dot-achievements

Three small addons, read together because they answer one question (E12).

| Repo | Commit | Licence | Read |
|------|--------|---------|------|
| https://github.com/modcommunity/dot-stats | `5a2519c` 2026-09-26 | MIT | `README.md` 14-64 |
| https://github.com/modcommunity/dot-leaderboard | `adf3797` 2026-09-24 | MIT | `README.md` 14-79 |
| https://github.com/modcommunity/dot-achievements | `6e213da` 2026-09-24 | MIT | `README.md` 14-63 |

NOT READ: code, `CLAUDE.md` of all three, their suites. Validation of submitted records on the backbone side is not
in these repositories (the backbone is closed source, per every README's licence paragraph); `UNREACHABLE`.

## Keys

- Stats are filed "under the SCOPED key, never the account id" (`dot-stats/README.md:27-47`). game-playground files
  under the scoped pseudonymous id when it has an identity stack, otherwise under the session id
  (`game-playground/CLAUDE.md:957-963`).
- Boards are "an ordering over one number per player, scoped by string keys", e.g.
  `{"map": "surf_beginner", "track": "0", "style": "normal"}` (`dot-leaderboard/README.md:14-17,33-55`); game-hungario
  scopes boards by mode (`game-hungario/CLAUDE.md` "Boards").

## Recording

- A game declares each stat once with a **merge kind** (counter adds, gauge kept and not reported, best/lowest keep the
  better); deltas are reported every 30 s and on the way down (`dot-stats/README.md:17-47`).
- Boards: four orderings (TIME, SCORE, POINTS, PENALTY); ranks materialised on write; a store interface with an
  in-memory implementation; batched reporter that survives an outage and is bounded (`dot-leaderboard/README.md:19-25`).
- Achievements are **data**, so a server "can validate the whole catalogue at boot" without loading assets
  (`dot-achievements/README.md:17-19`). The stats link converts session totals into lifetime deltas, because wiring the
  raw signal "adds the running session total on every kill: two after the second, five after the third, nine after the
  fourth" (`dot-achievements/README.md:53-62`).
- Recorded from signals the game already fires; a second count is "a second number that can disagree"
  (`game-playground/CLAUDE.md:917-920`).
- Reporting is refused from peer-to-peer sessions (`game-playground/game/playground_party.gd:111-117`).

## The equivalent for a party-game web app with no accounts (E12)

`INFERRED`: the honest key for us is the **seat** (`RoomPlayer.Id` or `(RoomId, Name)`), which lives exactly as long as
the room, like their session-id fallback. A "tonight's scoreboard" is a board scoped by room (and optionally by date),
filled from each game session's final result at completion, from the one place completion already happens
(`GameSessionService.Complete`, `backend/RandomRoom.Api/Services/GameSessionService.cs:144-148`). Their per-mode scoping
argues for keeping per-game scores separate and combining **placings**, not raw points, which matches the open question
in our plan ("raw points do not compare; placings do").
