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

Open http://localhost:5174. Calls to `/api` are proxied to the API on port 5080. Sign in with any email and password to reach the demo dashboard (no real auth yet).

## Build and test

```bash
dotnet build backend/InternshipCoach.slnx
dotnet test backend/InternshipCoach.slnx
cd frontend && npm run build
```
