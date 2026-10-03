# Agent prompt: preflight check (run this first, while you are awake)

How to use: start a **cloud** agent session on `AmosQuety/Random-Room` and paste everything below the line. It takes
about 10 to 15 minutes, changes nothing except one small file on one branch, and ends with a verdict: `READY`,
`READY WITH LIMITS` or `NOT READY`, plus exactly what to fix. Read the verdict, fix what it lists, and only then start
the real run (`explore-game-platform-repos.md`).

---

# TASK: PREFLIGHT CHECK. DO NOT START ANY RESEARCH.

You are checking whether this environment can run a long, unattended research job. You will **test** each capability
below, record the exact outcome, and report. You will **not** begin the research. Do not ask questions; when a step
fails, record the exact error (with any secrets removed) and continue with the next step. Finish within about 15
minutes.

## Rules
1. Do not print, read out, log or commit any environment variable value, token, credential or `.env` file. You may
   print the **names** of tools, never secret values.
2. Do not run code from any repository you download. Reading files is fine.
3. Do not contact anything except `github.com`, `raw.githubusercontent.com`, `codeload.github.com` and
   `api.github.com`. In particular, **do not touch `moddingcommunity.com`** (it blocks automated visitors; do not try).
4. The only thing you may write to the repository is the file `docs/research/preflight.md`, committed to the branch
   `research/game-platform-report` (step 8). Never push to `main` or any other branch. Never force-push. Do not open a
   pull request. Do not add any AI attribution (no `Co-Authored-By`, no "Generated with") to the commit or the file.
5. If something is denied or blocked, do not look for a way around it. Record it and move on.

## Steps (record result, exact error text, and time taken for each: PASS / FAIL / BLOCKED)

1. **Where am I.** `pwd`, `git rev-parse --show-toplevel`, `git remote -v` (hide any credentials in URLs),
   `git branch -a`, `git status --short`, `git log -1 --oneline`. Is this `AmosQuety/Random-Room`? Which branch is
   checked out?
2. **Git identity.** `git config user.name` and `git config user.email` (print them; they are not secret). Are they set?
3. **Tools.** For each of `git curl jq grep find sed awk wc sort python3 node tar gh rg`, report whether it exists
   (`which`). List the tools you have available to you by name (shell, file read, file write, web fetch, sub-agents,
   anything else). Say which of them asked for permission or were denied when used in this check.
4. **Machine limits.** Free disk space where you will work (`df -h .` and the temp folder), whether the temp folder is
   writable (`${TMPDIR:-/tmp}`), memory (`free -h` if present). Report any limit your tooling tells you about (session
   length, token budget, number of tool calls) if you know it; if you do not know, say "unknown".
5. **Network to GitHub.** Test each and record PASS/FAIL with the error:
   - `git ls-remote https://github.com/gamemann/game-playground`
   - `git clone --depth 1 https://github.com/gamemann/game-playground` into `${TMPDIR:-/tmp}/preflight/` and report the
     size (`du -sh`) and the number of files; then delete it
   - `curl -sI https://codeload.github.com/gamemann/game-playground/tar.gz/HEAD | head -1`
   - `curl -s https://raw.githubusercontent.com/gamemann/game-playground/HEAD/README.md | head -3`
   - `curl -s -o /dev/null -w "%{http_code}\n" https://api.github.com/orgs/modcommunity/repos?per_page=1` and the
     remaining rate limit (`curl -sI https://api.github.com/rate_limit` or the `x-ratelimit-remaining` header)
   - the web-fetch tool, if you have one, on `https://github.com/gamemann/game-playground` (PASS if you get content)
6. **Find the sibling repositories** (this saves the real run time and tells us the names are right). Using
   `git ls-remote https://github.com/<owner>/<name>` (exit code 0 means it exists), test these names under **both**
   owners `modcommunity` and `gamemann`, and list which exist and under which owner: `dot-core`,
   `dot-player-controller`, `dot-timer`, `dot-map`, `dot-props`, `dot-leaderboard`, `dot-server`, `dot-net`,
   `dot-inventory`, `dot-ui`, `dot-vote`, `dot-npc`, `dot-npc-ai`, `dot-chat`, `dot-stats`, `dot-achievements`,
   `dot-user`, `dot-user-avatar`, `dot-combat`, `dot-match`, `dot-loadout`, `dot-randomness`, `dot-cloud`, `dot-ci`,
   `dot-server-deploy`, `dot-peer-to-peer`, `dot-p2p`, `zee-dot-weapons`, `game-simple-lobby`, `game-hungario`,
   `game-arena`, `game-g2gfast`, `game-playground`. Also try the API listing
   `curl -s "https://api.github.com/orgs/modcommunity/repos?per_page=100"` and
   `curl -s "https://api.github.com/users/gamemann/repos?per_page=100"` and add any extra repository names you see whose
   name starts with `dot-` or `game-`. For each repository that exists, also record the licence file name if you can see
   it cheaply, and nothing else.
7. **Our files are readable.** Check that each of these exists in the checkout and report any that are missing:
   `README.md`, `DECISIONS.md`, `NEXT_STEPS.md`, `Gaps_Bugs.md`, `PROGRESS.md`, `docs/plan-switch-games-in-a-room.md`,
   `docs/agent-prompts/explore-game-platform-repos.md`, `backend/RandomRoom.Api/Games/IGameEngine.cs`,
   `backend/RandomRoom.Api/Services/GameSessionService.cs`, `frontend/src/games/types.ts`, `docs/QA_PROMPT.md`.
   The `docs/plan-switch-games-in-a-room.md` and `docs/agent-prompts/` files are expected to be missing **if the docs
   pull request is not merged yet**. In that case check that the branch `docs/future-plans-and-research-prompt` exists
   on the remote (`git ls-remote origin docs/future-plans-and-research-prompt`) and that you can read the plan with
   `git show origin/docs/future-plans-and-research-prompt:docs/plan-switch-games-in-a-room.md | head -5` after
   `git fetch origin docs/future-plans-and-research-prompt`.
8. **Push test (the important one).** Create the branch `research/game-platform-report` from `origin/main`
   (`git fetch origin main` then `git checkout -b research/game-platform-report origin/main`; if it already exists on
   the remote, check it out instead). Create `docs/research/preflight.md` containing a short summary of steps 1 to 7 (no
   secrets). Run `git add docs/research/preflight.md`, confirm with `git status --short` that **only that file** is
   staged, `git commit -m "docs(research): preflight check"`, then `git push -u origin research/game-platform-report`.
   Report PASS or FAIL with the exact error. If it fails, **try nothing else** (no other branch, no force, no other
   remote); just record it.
9. **A restart-safety check.** Say, from what you can observe, whether files you create outside the repository (in
   `${TMPDIR:-/tmp}`) would survive a session restart (you may not be able to tell; say "unknown"), and whether the
   pushed branch is the only thing that would.

## Output

1. The file `docs/research/preflight.md` (committed and pushed in step 8, or left uncommitted if the push failed) with
   the full results table.
2. Your **final message**, which must contain exactly these parts:
   - **Verdict:** `READY`, `READY WITH LIMITS` or `NOT READY`.
   - **Results table:** one line per step 1 to 9 with PASS / FAIL / BLOCKED and a few words.
   - **What the owner must fix** (a numbered list, most important first, each with the exact error text), or "nothing".
   - **Limits you noticed** (disk, memory, session length, tool permissions, rate limits).
   - **Sibling repositories found** (owner/name list) and the ones not found.
   - **Branch pushed:** yes/no, with the commit hash if yes.
   - **Anything surprising** about the environment.

Use this rule for the verdict: `NOT READY` if you cannot reach GitHub at all, cannot push the branch, or cannot write
files; `READY WITH LIMITS` if everything works but something is restricted (for example the API rate limit is very low,
a tool needs a permission prompt, `jq` is missing, clones are slow or small); otherwise `READY`.

Then stop. Do not start the research.
