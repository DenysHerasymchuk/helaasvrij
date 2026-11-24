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

Stop with `Ctrl+C`, or `docker compose down` (add `-v` to also delete the database).

## Frontend development (hot reload)

Keep the Docker stack running for the API, then in a second terminal:

```bash
cd frontend
npm install
npm run dev -- --port 5174
```

Open http://localhost:5174. Calls to `/api` are proxied to the API on port 5080. Register a real account (email and password, or GitHub) to reach the dashboard; the dashboard data itself is still demo data.

## Build and test

```bash
dotnet build backend/InternshipCoach.slnx
dotnet test backend/InternshipCoach.slnx
cd frontend && npm run build
```
