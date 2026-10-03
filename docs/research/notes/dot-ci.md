# dot-ci

- URL: https://github.com/modcommunity/dot-ci. Commit `d26b5a4`, 2026-10-03 (it moved during the session; preflight
  saw `8a41ce6` 2026-09-26). Licence: MIT. 112 KB, 12 files: three workflows, four scripts, `godot.pin`.
- Read: `README.md` 48-114; `CLAUDE.md` 7-48. NOT READ: the workflow YAML and script bodies.

## Purpose (F14)

"The checks and the release packaging every project in this family shares, as two reusable GitHub Actions workflows
and four shell scripts" (`README.md` first line). Rule: "Sixty repositories, one copy of the knowledge. Every caller's
workflow file is a `uses:` line and a secret" (`CLAUDE.md:7-11`).

## What a check does

1. Every tracked script parses (`--import`, then `--check-only` per file from `git ls-files`).
2. Every suite runs, each capped by a timeout, because a scene whose script fails to parse **hangs** rather than failing.
3. A suite that exits 0 is still read for `SCRIPT ERROR` / `Parse Error`, because an error inside a test aborts that
   test only: "nine sections, sixty-three passed, zero failed, exit 0, eight checks that never ran"
   (`README.md:69-77`).
4. Suites auto-detected as `examples/*selftest*` or `examples/headless_*`; a repository with **no detectable suite is
   reported and passes** ("Failing them instead would only teach people to delete the workflow") (`README.md:79-81`).
   game-simple-lobby found its two real-server suites never ran in CI because of this detection
   (`game-simple-lobby/.github/workflows/ci.yml` comment).

## Release, versioning, packaging (A2, A3, F14)

- Version comes from the tag ("the only value that cannot disagree with what was pushed"); `plugin.cfg`'s version is
  stamped from the tag **in the artifact only** (`README.md:62,85`).
- Addon repository: ships `addons/<name>/` as a zip that installs by unpacking at a project root. Game: ships the tracked
  tree as a tarball; with `pack: true` also `<name>-<version>-pack.zip` (tree minus `addons/ examples/ tools/
  screenshots/ images/` plus imported outputs). Both with `SHA256SUMS`. Built with `git archive` so gitignored addon
  symlinks are never shipped (`README.md:83-93`).
- **A release does not publish and does not sign**: "a mounted resource pack can contain scripts and can never be
  unmounted ... The private half of that key exists in one place on purpose" (`README.md:95-99`).
- `package.sh --pack` writes `requires.json`: which addon API levels the game's files use; dot-cloud reads it before
  mounting (`CLAUDE.md:47-48`).
- Godot runtime pinned with a SHA-512 in git, never read from beside the binary (`README.md:101-107`).

## Compare

Our CI (`.github/workflows/ci.yml`) runs on pull requests only: backend tests with Postgres, lint, contrast, build,
vitest. No release or version stamp. game-playground's comment explains why they also run on every branch push
(`game-playground/.github/workflows/ci.yml` header). Our backend and frontend share one repository, so most of the
"one copy across sixty repositories" problem does not exist for us. NOT COMPARABLE in scale. What transfers: (a) a
build/version stamp the client can compare with the server's; (b) read test output for errors even when the runner
exits 0 is not needed for xUnit/Vitest, which fail per test (EQUIVALENT).
