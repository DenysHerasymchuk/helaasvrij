# Internship Coach

## Requirements

- Docker with Docker Compose (your user in the `docker` group, or use `sudo`)
- Node.js 22+ and npm (only for frontend development)
- .NET 10 SDK (only for backend builds and tests)

## Setup

```bash
git clone https://github.com/DenysHerasymchuk/internship-coach.git
cd internship-coach
git checkout develop
cp .env.example .env
```

Open `.env` and replace `CHANGE_ME` with a database password of your choice. Never commit `.env`.

Other keys in `.env` (all optional for a first start, the app runs without them):

- `R33_API_KEY` -> route33 LLM key. Without it every AI feature (profile extraction, gap matrix, drafts, interview prep, chat) answers with an error.
- `GITHUB_TOKEN` -> raises the GitHub ingest limit from 60 to 5,000 requests per hour (no scopes needed).
- `GITHUB_CLIENT_ID`, `GITHUB_CLIENT_SECRET` -> "Sign in with GitHub" (see below).
- `ADZUNA_APP_ID`, `ADZUNA_APP_KEY` -> search real postings on the Postings page.

## GitHub sign-in (optional)

Email and password work without this. To enable "Sign in with GitHub":

1. On GitHub open Settings, Developer settings, OAuth Apps, New OAuth App.
2. Homepage URL `http://localhost:5173`. Authorization callback URL `http://localhost:5173/api/auth/github/callback`.
3. Generate a client secret and put both values in `.env`:

```
GITHUB_CLIENT_ID=your-client-id
GITHUB_CLIENT_SECRET=your-client-secret
```

4. Restart: `docker compose up -d api`.

For the Vite dev server (port 5174) create a second OAuth App with callback `http://localhost:5174/api/auth/github/callback` and use its keys in a local `.env`. Never commit real secrets. Without the keys, the GitHub button returns to the login page with an error message.

## Run everything (Docker)

```bash
docker compose up --build
```

| What | URL |
| --- | --- |
| App | http://localhost:5173 |
| API health | http://localhost:5080/api/health |
| Swagger | http://localhost:5080/swagger |

In development the API creates a demo student and four demo postings on first start. Sign in with `lotte@demo.test` / `Demo1234!` to try the app without registering.

Typical first walk-through: Profile (add a GitHub username, paste CV text, confirm items), Postings (open a demo posting, Analyse), Gap plan (tick a gap to re-scan), Documents (CV, cover letter, recruiter message), Interview prep.

Stop with `Ctrl+C`, or `docker compose down` (add `-v` to also delete the database).

## Frontend development (hot reload)

Keep the Docker stack running for the API, then in a second terminal:

```bash
cd frontend
npm install
npm run dev -- --port 5174
```

Open http://localhost:5174. Calls to `/api` are proxied to the API on port 5080. Register an account or use the demo student above.

## Build and test

```bash
dotnet build backend/InternshipCoach.slnx
dotnet test backend/InternshipCoach.slnx
cd frontend && npm run build
```
