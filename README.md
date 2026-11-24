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

## AI fallback with Claude Code

route33 is always tried first. With `LLM_FALLBACK=claude-cli` in `.env`, a failed route33 call (no key, 401/403, 429, 5xx, timeout, network error) is retried once through your own Claude Code CLI (`claude -p`, billed to your Claude subscription). Sonnet answers Smart calls, Haiku answers Cheap calls; the CLI runs with no tools. The API log says which transport answered, never the content.

The API container has no `claude` binary, so a small bridge on the host runs it:

```bash
# On the host, with `claude` installed and signed in. Pick a long random token.
CLAUDE_BRIDGE_TOKEN=$(openssl rand -hex 24) BRIDGE_HOST=0.0.0.0 node scripts/claude-bridge.mjs
```

Put the same token in `.env` as `CLAUDE_BRIDGE_TOKEN=...`, keep `LLM_FALLBACK=claude-cli`, then `docker compose up -d --build api`.

Warning: `BRIDGE_HOST=0.0.0.0` lets any machine that can reach port 8787 use your Claude subscription if it has the token. Only do this on a trusted network, or bind to the Docker bridge instead (`BRIDGE_HOST=172.17.0.1`, the address `host.docker.internal` resolves to). Without `BRIDGE_HOST` the bridge listens on `127.0.0.1` only. It rejects requests without the token, handles one call at a time and caps request bodies at 2 MB. If the container cannot reach it, check that your firewall allows port 8787 from Docker.

With `dotnet run` on the host no bridge is needed: set `COACH_Llm__Fallback=ClaudeCli` and `COACH_Llm__ClaudeCli__Mode=Local` (the default mode). Optional: `COACH_Llm__ClaudeCli__TimeoutSeconds` (default 120) and `COACH_Llm__ClaudeCli__Command` (default `claude`).

Tests: `node --test scripts/claude-bridge.test.mjs` (fake `claude`), and `CLAUDE_CLI_SMOKE=1 dotnet test backend/InternshipCoach.slnx --filter ClaudeCliSmokeTests` for one real local call.

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
