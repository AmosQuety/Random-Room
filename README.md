# Random Room

"Who do we choose?" - four fixed players share one room. Each one presses a button, and the
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
