# dot-vote

- URL: https://github.com/modcommunity/dot-vote. Commit `e7b0c5e`, 2026-09-26. Licence: MIT. 536 KB, 49 files,
  ~6,450 lines of addon code plus a 3,442-line self-test.
- Read: `README.md` (all), `CLAUDE.md` (all, 179 lines), `core/dot_vote_choice.gd` 1-143, `core/dot_vote_rules.gd`
  enums (35-160), `runtime/dot_vote_history.gd` 73-113, function list of `runtime/dot_vote_source.gd`.
- NOT READ line by line: `dot_vote_director.gd` (1,884 lines), `dot_vote_ballot.gd` (796), `dot_vote_clock.gd` (504),
  `dot_vote_nominations.gd`, `dot_vote_commands.gd`, `docs/parity.md`, the self-test body. Behaviour of those is taken
  from README and CLAUDE.md, which describe it in detail. "Read fully" was not achieved for the code; it was for the docs.

## Purpose

"What you add when the players, rather than the rotation, should decide what runs next", whether games, maps or modes
(`README.md` first lines and "Voting For What Plays Next"). A rebuild of the community map-chooser plugin family
(`rtv`, `nominate`, `mapchooser`, `timeleft`, `extend`) where every policy is a setting (`README.md`).

## The design decision

A `DotVoteChoice` is "an id, a name, and the handful of facts a ballot needs", not a game or a map
(`CLAUDE.md` "The one design decision"; `core/dot_vote_choice.gd:4-16`). Fields: `id` ("Never renamed. History,
cooldowns and per-choice configuration are all keyed on it"), `display_name`, `description`, `group`, `enabled`,
`min_players`, `max_players`, `nominate_only`, `official`, `time_limit_sec` (negative = use rules, 0 = no limit),
round limit, cooldown, weight (`dot_vote_choice.gd:23-110`). `available_for(players, nominated)` filters by enabled,
nominate-only, min and max players (`127-140`).

What a choice *means* is a `DotVoteSource`: `choices()`, `current_id()`, `apply(id)`, `supports_apply()`
(`runtime/dot_vote_source.gd:20-87`). Shipped sources: `DotVoteGameSource.of(server.games)` (apply = dot-server
`change_game`), `DotVoteMapSource`, `DotVoteListSource` (any list with a `Callable`). Duck-typed so the addon depends
only on dot-core (`README.md` "Pointing it at something", "Dependencies"). Per-choice settings live in the thing's own
metadata under a `vote:` key, e.g. in `game.yml` (`README.md`).

## Policies (B4)

From `core/dot_vote_rules.gd` enums and the README table:

| Area | Options |
|------|---------|
| Trigger | TIME_LIMIT, ROUND_END, RTV_ONLY, MANUAL, SCORE_LIMIT (`rules.gd:35-51`) |
| Ballot fill | RANDOM, WEIGHTED, SEQUENTIAL, LEAST_RECENTLY_PLAYED, MOST_NOMINATED (55-66); "extend", "don't change", "no vote" as optional pseudo-options; reserved nomination slots "so three organised players cannot decide every map" (`README.md`) |
| Counting | PLURALITY, MAJORITY_RUNOFF, INSTANT_RUNOFF, APPROVAL (70-80); quorum; weighted ballots (`weight_fn`) |
| Ties | BALLOT_ORDER, NOMINATION_ORDER, LEAST_RECENTLY_PLAYED, RANDOM, RUNOFF (89-101). Ties are broken in "choices-first" order, never the display order, so moving "don't change" to the top of a menu cannot make every tie go to the status quo (`CLAUDE.md` "Presentation is not policy") |
| No quorum / no votes | WINNER_ANYWAY, KEEP, ROTATION / KEEP, RANDOM, ROTATION (105-151) |
| Apply | IMMEDIATE, END_OF_ROUND, END_OF_TIME, with a delay so players can read the result (115-121) |
| Cooldown | PLAYS or MINUTES, per choice, **clamped to a share of the pool** "A cooldown of eight on a rotation of six otherwise excludes everything" (`runtime/dot_vote_history.gd:73-111`) |
| Rock the vote | fraction, minimum players, delay, idempotent per player, withdrawn on disconnect; outcome OPEN_VOTE / CHANGE_NOW / END_CURRENT (133-140) |
| Extend | amount, max extends; "extend" leaves the ballot when used up (`CLAUDE.md` bugs) |
| Admin | `setnextmap`, `forcertv`, `endvote`, `extend`, nomination bypass; admin commands need `changemap` (`README.md`, `CLAUDE.md`) |
| Vetoes | **None.** No veto setting or code (grep for "veto" in `addons/` finds nothing). An admin's override is `setnextmap` / `endvote`. |

Eighty settings in `DotVoteRules`, layered defaults < game.yml metadata < file < `DOT_VOTE_*` < `--vote-*`; a layered
result that does not validate is refused whole (`README.md` "Configuring it"; `CLAUDE.md` table). The self-test fails if
any setting is read by nothing ("Every setting is read by something") (`CLAUDE.md` "Validating").

`DotVoteResult` records **how** a result was decided, including which tie-break, "so the announcement can explain
itself" (`README.md` "The pieces").

`DotVoteClockView`: the clock as a client shows it, sent when it changes and counted down between; empty when there is
no limit (`README.md`).

What it deliberately does not have: a wire format, UI, persistence (history serialises to a dictionary; the host stores
it if wanted), per-choice permissions (`CLAUDE.md` "Things deliberately not here").

## Bugs found building it (`CLAUDE.md` "Bugs found by building it")

| # | Bug | Lesson | Relevance to our next-game vote |
|---|-----|--------|---------------------------------|
| V1 | A choice with no time limit never counted elapsed time, so rock-the-vote was refused forever on exactly the server that depends on it | assert the internal value, not only the symptom | A "no time limit" game must still be switchable by vote |
| V2 | `extend_needs_majority` decided nothing in either position | "A setting that reads differently and behaves identically" | test each policy both ways |
| V3 | MOST_NOMINATED could never do anything: seconding refused | a fill mode depends on another setting | — |
| V4 | A vote that could not open (cooldown, too few players) was never offered again | latched triggers need retry | **Yes**: if the room has too few players for any game, the vote must reopen later, not die |
| V5 | `await` through reflection had to be verified | test the integration for real | — |
| V6 | `rtv_only` still opened a ballot at the time limit | trigger semantics | — |
| V7 | Admin commands used a command name as a permission flag; only root could run them | test permission strings | Low |
| V8 | "Extend" stayed on the ballot after extensions ran out | ballot options must reflect state at opening | **Yes**: "play again" options must reflect what is allowed |
| V9 | A nomination made during an open ballot was accepted then thrown away | refuse and say why | **Yes**: a proposal during a vote |
| V10 | `time_limit` and `round_end` triggers behaved identically | "reads differently, decides nothing" | — |

Plus two found by consumers (game-playground, game-hungario): the director self-advanced **and** was advanced by the
module, so every vote clock ran at double speed; and `begin_on_apply` made one play count twice in the history,
halving every cooldown (`game-playground/CLAUDE.md:925-934`; `game-hungario/game/hungry_maps.gd:282-287`).

## What transfers to a party-game web app

The model (choice = id + facts; source = what it means; director = state machine; history = cooldowns) maps directly
onto a "next game" ballot in a room: choices are our 19 `GameType` keys with `min_players`/`max_players` from the
engines; the source's `apply` is our proposed "change game" call; history is the room's list of sessions. Most of the
80 settings exist because community servers run unattended for days with strangers; a friends' room with a host needs
very few: plurality, a short ballot, host confirm or override, and "do not repeat the last game". `INFERRED`.
