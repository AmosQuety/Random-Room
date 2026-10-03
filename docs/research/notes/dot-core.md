# dot-core

- URL: https://github.com/modcommunity/dot-core. Commit `fcce93f`, 2026-09-26. Licence: MIT. 696 KB, 92 files.
- Read: `README.md` first paragraph; `CLAUDE.md` headings, lines 57-67 (`DotResult`), 180-195 (layered configuration).
- NOT READ: code; the other `CLAUDE.md` sections (autoloads, `DotNodeRef`, platforms, randomness, conventions).

## Purpose

"The foundation for all other `dot-*` assets" (`README.md`).

## Relevant mechanisms

- **Layered configuration** (`DotConfig`): exported defaults < `.tres`/`.json` file < environment < command line, later
  wins; discovery is reflective so a new setting needs no registration; key spellings `max_players`, `maxPlayers`,
  `max-players` normalise to one property; unknown keys are warned about, "never fatal — refusing to boot because a
  config mentions a setting from a newer version is worse than ignoring it"; `sensitive_keys()` cannot be set from
  environment or argv because "both are readable by other processes and end up in `ps` output and bug reports"
  (`CLAUDE.md:180-194`). Validation is per subclass (`validate()`, see `game-playground/game/playground_config.gd:111-124`).
- **`DotResult`** for every fallible operation, with `DotError.CODE_*`; "callers branch on the code, never on the
  message"; `wrap()` adds context without discarding the cause (`CLAUDE.md:57-67`).
- Non-negotiables include "No autoloads. Ever." and "Never hardcode a scene path" (headings, `CLAUDE.md:18,35`; bodies
  NOT READ).

## Comparison

Our equivalent of `DotConfig` is ASP.NET Core configuration (appsettings < environment, with the `Section__Key`
convention), which already layers the same way. Our equivalent of `DotResult` + codes is `RoomRuleException` with a
`RuleViolation` code mapped to an HTTP status (`backend/RandomRoom.Api/Services/RoomRuleException.cs`,
`ProblemExceptionHandler.cs`). EQUIVALENT; nothing to adopt.
