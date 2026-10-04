# dot-map and dot-match

| Repo | Commit | Licence | Read |
|------|--------|---------|------|
| https://github.com/modcommunity/dot-map | `42e45fc` 2026-09-25 | MIT | `README.md` 14-17, 80-129 |
| https://github.com/modcommunity/dot-match | `1cd016f` 2026-09-27 | MIT | `README.md` 42-71 |

NOT READ: code and `CLAUDE.md` of both.

## dot-map (B4)

"So one game can carry a hundred maps": a catalogue, a rotation with a cooldown, a map time limit with rock-the-vote and
extend, nominations and voting, and a loader (`README.md:14-17`). The catalogue is not the rotation; the rotation's
`pool(player_count)` filters by players and cooldown; a simple `DotMapVote` (plurality, one tie-break) sets the next
map (`README.md:80-94`). Ending a map is the session's signal; "What happens next is yours" (`README.md:96-112`).
Changing the map for everybody: announce, fetch, ready, then load; "A host may say which map, never what the map is"
(`README.md:114-128`). dot-vote is the general engine; dot-map's own vote remains for simple cases
(`dot-vote/CLAUDE.md` "Why it is a separate addon").

## dot-match (B6, E12)

A match is a state machine `WARMUP -> COUNTDOWN -> LIVE -> INTERMISSION -> MATCH_END`, moved by one call per tick, no
timers or wall clock; what winning means is a `DotMatchRules` resource (`README.md:42-48`). Three failure modes it is
built around (`README.md:64-70`):

1. "A scoreboard that rewards rage-quitting": records survive disconnection, keyed by a stable player key, never a peer
   id.
2. "Two machines that disagree": every ordering has an explicit tie-break (scoreboards fall back to the player key).
3. "A round that never ends": zero teams standing ends the round.

## Compare

Our phase machine (`PhaseGuard`: lobby -> collecting -> revealed -> collecting | complete,
`backend/RandomRoom.Api/Games/Shared/PhaseGuard.cs:18-25`) and session status (Waiting -> Active -> Completed,
`Domain/GameSession.cs`) are the same idea, driven by actions and a lazily checked deadline instead of ticks.
Our scoreboard rule "ties share a rank, next rank skips" (`DECISIONS.md` "Shared `Scoreboard` extracted now") is a
deterministic tie rule; it lives in the frontend component. Scores are keyed by player name, which is stable for the
room's life. EQUIVALENT. Their point 3 maps to ours as: every game must be able to complete even if players leave;
NOT CHECKED here per game.
