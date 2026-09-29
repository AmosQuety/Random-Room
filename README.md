# The Playground

Live party games for reunions, cell groups and game nights. The original mode, Random Picker, works like this:
"Who do we choose?" - players share one room. Each one presses a button, and the
**server** picks Sarah or Judith with a cryptographically secure RNG and shows it to everyone.
Nobody votes; nobody can choose or change a result. Spec: `random-room-mvp-spec.md`.

## Auth model

There are no accounts. Each of the four players has a secret PIN kept in `backend/RandomRoom.Api/.env`
(locally) or in the host's environment variables (production). Entering name + PIN once gives the browser
a signed 12-hour token, so the PIN is not re-sent with every action.

## Run locally

    # one-time: a Postgres for local development (port 5433 avoids clashing with another Postgres)
    docker run -d --name randomroom-pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=randomroom -p 5433:5432 postgres:16

    # one-time: create your secrets file, then fill in PINs and a signing key (openssl rand -base64 48)
    cp backend/RandomRoom.Api/.env.example backend/RandomRoom.Api/.env

    # terminal 1 - API on :5184 (creates/updates the database schema on start)
    cd backend/RandomRoom.Api && dotnet run --no-launch-profile --urls http://localhost:5184

    # terminal 2 - UI on :5173 with hot reload (proxies /api and /hubs to the API)
    cd frontend && npm install && npm run dev

Open http://localhost:5173/room/who-do-we-choose in several browsers/profiles.

## Test

    cd backend && dotnet test          # needs the Postgres container above; each test uses a throwaway database
    cd frontend && npx vitest run && npx tsc -b

## Deploy on Render

The repo root `Dockerfile` builds the UI and API into one container.

1. Create a **PostgreSQL** database on Render. Copy its **Internal Database URL**.
2. Create a **Web Service** from this repo, runtime **Docker**.
3. Add these environment variables to the web service:

       Room__PlayerPins__Amos=...   Room__PlayerPins__Lydia=...
       Room__PlayerPins__James=...  Room__PlayerPins__Jacob=...   (4+ characters each)
       Room__JwtSigningKey=...                                     (32+ random characters)
       ConnectionStrings__Default=<the Internal Database URL>      (postgres:// URLs are accepted as-is)
       Room__HostPlayer=Amos                                       (optional)

   The app refuses to start if the PINs or signing key are missing.
4. Share `https://<your-service>.onrender.com/room/who-do-we-choose`, and send each player their PIN privately.

Notes:
- Run a **single instance**: online/offline presence is kept in memory.
- Render's free web tier sleeps after ~15 min idle, so the first visit can take up to a minute to wake it.
  Room history lives in Postgres and is unaffected. Render's free Postgres expires after 30 days - use a
  paid instance if you need it to last.

## Games

| Game | Category | Notes |
| --- | --- | --- |
| Random Picker | Reflex & Chance | Everyone triggers a server-random pick from a shared list |
| Trivia | Quiz | Host-curated multiple choice with a scoreboard; optional categories, a 30-question starter bank, and a per-question timer |
| Would You Rather | Poll & Reveal | Private two-way pick; sit with the majority to score. 30 built-in dilemmas |
| This or That | Poll & Reveal | Rapid pairs, same scoring. 30 built-in pairs |
| Most Likely To | Poll & Reveal | Vote for a friend (not yourself); the most-voted is crowned. 30 built-in prompts, 3+ players |
| Never Have I Ever | Poll & Reveal | Clean statements; "still standing" scoring. 30 built-in statements |
| Survey Showdown | Poll & Reveal | Guess the hidden survey board; matching an answer scores its points. 15 built-in surveys |
| Two Truths and a Lie | Quiz | Each player is storyteller once; the others vote on the lie. 3+ players |
| Guess Who Wrote It | Quiz | Everyone writes a fact about themselves; the group guesses who wrote each. Text only. 3+ players |
| Name That Song or Movie | Quiz | The host writes clues (text, emoji, riddles) with accepted answers and an optional https link; the server marks guesses. First correct gets a bonus |
| Spin the Wheel | Reflex | Take turns spinning a wheel of challenges. The server decides where it lands; the host can award a point. 2+ players |
| Buzzer Round | Reflex | The host reads a prompt; the first to buzz answers and the host judges. Server decides who was first. 3+ players |
| Bingo | Reflex | Each player gets their own card. The host calls items, you mark them, and the server checks every claim. 2+ players |
| Fill-in Stories | Story | Everyone fills a few blanks of a story they cannot see, then it is read out. 30 built-in stories or your own. 2+ players |
| One-Word Story | Story | Build a story one word per turn from a random opener. 2+ players |
| Fortunately / Unfortunately | Story | A story that swings between good luck and bad, one sentence per turn. The game supplies the lead-in. 2+ players |
| Word Spies | Word | Two teams, one grid of words. Only each spymaster sees the key and gives one-word clues; avoid the assassin. 4+ players |
| Forbidden Words | Word | Describe a secret word without saying the forbidden ones; everyone else guesses and a judge who sees the card flags slips. 3+ players |
| Sketch Guess | Word | One player draws a secret word on a shared canvas while everyone else guesses; only the drawer sees the word. 2+ players |

## Adding a game

Trivia was added as the proof of the plugin seam; copy its shape.

**Prompt-and-answer games (the easy path).** If the game is "a prompt appears, everyone answers privately, then it
reveals" (polls, votes, guesses), you do not write an engine. Write a small rules class in
`Games/Rounds/` implementing `IRoundRules<TPrompt>` (parse a host prompt, view a prompt, validate an answer,
resolve a round into a summary and points), add an embedded bank in `Games/Content/<name>.json`, and register it
with `services.AddRoundGame<TPrompt, TRules>()`. On the frontend, build the module from `games/rounds`
(`roundSetupFor`, `RoundSetupForm`, `RoundGameScreen`); see `games/would-you-rather` for the smallest example.
Phases, hidden answers, one answer per player, deadlines, scoring totals and restart recovery come for free.

**Anything else** uses the shared primitives in `Games/Shared/`: `GameStore` (setup, session state, entries, locks),
`PhaseGuard`, `ServerTimer`, `AnswerNormalizer`, `ContentBank`; override `GetPayloadForAsync` for per-player views.

Backend (`backend/RandomRoom.Api`):
1. Add domain entities under `Domain/` if the game persists state, and register them in `Data/RoomDbContext.cs`.
2. Implement `IGameEngine` in `Games/<Name>/` (validate setup, build the per-player payload, handle actions with `RoomRuleException` for illegal moves; never put hidden information in a payload for someone not entitled to it).
3. Register the engine in `Program.cs`, then create one additive migration with `dotnet ef migrations add`.
4. Add xUnit tests next to `TriviaEngineTests.cs`: happy path, wrong phase, wrong actor, duplicate submission, invalid input, no leaks, completion.

Frontend (`frontend/src/games/<name>/`):
1. `types.ts` for the setup and payload shapes, `SetupForm.tsx`, `GameScreen.tsx`, and a `Glyph.tsx` drawn with token classes (`fill-accent`, `stroke-ink`).
2. `index.ts` exporting a `GameModule`: `key`, `name`, `hook`, `category`, `accent`, `minPlayers`/`maxPlayers`, setup helpers, and `React.lazy` for `SetupForm` and `GameScreen`.
3. Add it to `games/registry.ts`. The picker, categories and player-count messages pick it up automatically.
4. Use the shared `Scoreboard`, `Button`, `Card`, `Field` and `ListEditor` components; keep colours as tokens from `src/index.css`.

`npm run check:contrast` verifies every text/background pairing against WCAG AA.
