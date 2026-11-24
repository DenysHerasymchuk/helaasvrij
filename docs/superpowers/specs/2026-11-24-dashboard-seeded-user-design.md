# Dashboard with a seeded demo user

## Intent

Show a real `/dashboard` inside the app layout (Untitled UI sidebar), filled from Postgres through the API, for one seeded demo student. Starts the real P0 data model (profile items with sources, postings, requirements, score in code) instead of a frontend mock.

## Goals

- Sidebar app layout with all items from `CLAUDE.md` (Dashboard, Postings, Gap plan, Documents, Interview prep, Applications, Profile, Settings).
- `GET /api/dashboard` returns the demo user's data from the database.
- Match score computed in Domain code from requirements, with a unit test.
- Every user-owned row has `UserId` and an EF global query filter.

## Non-goals

- No real auth. No ingest, no LLM, no re-scan. Other sidebar pages are placeholders ("Coming soon").
- No applications kanban entity.

## Anti-goals

- Score coming from seed data or the frontend instead of code.
- A profile item without a source.

## Approaches

- A. Frontend mock file: fastest, but no data model. Rejected by Denys.
- **B. Seeded user in Postgres, served by the API: picked by Denys.**

## Data model (Domain)

| Entity | Fields |
| --- | --- |
| `User` | Id, GitHubLogin, DisplayName, AvatarUrl, School, StageStart, StageEnd |
| `Source` | Id, UserId, Kind (GitHub, Cv, Site), Label, LastSyncedAt |
| `ProfileItem` | Id, UserId, Kind (Skill, Project, Experience, Language), Name, SourceRef, Quote, Confirmed |
| `Posting` | Id, UserId, Company, Title, Language (nl/fr/en), Deadline, InterviewAt?, IsDemo |
| `Requirement` | Id, PostingId, Text, Importance (Required, Nice), Coverage (Strong, Partial, None), EvidenceQuote?, EvidenceSource?, Action?, ActionProject?, DueDate?, Done |
| `ScoreSnapshot` | Id, PostingId, Score, TakenAt (for the trend line) |

`Posting.Score()` = sum(weight x coverage) / sum(weight); Required 2, Nice 1; Strong 1, Partial 0.5, None 0.

## Current user

`ICurrentUser` in Application. Infrastructure implementation returns the seeded demo user's id (`shortcut:` comment: replace with GitHub OAuth claim). The DbContext uses it in a global query filter on every `UserId` table.

## Seed

`DemoSeeder` in Infrastructure inserts the demo student if no user exists: synthetic persona (not a real person), 3 GitHub repos as sources and projects, about 10 profile items with quotes, 4 postings labelled demo (NL, FR, EN mix), requirements with coverage and actions naming the repos, a few score snapshots. Runs after `--migrate`. Compose `api` runs migrate+seed, then starts.

## API

`GET /api/dashboard` returns `DashboardResponse`: user (name, avatar, school, stage period), profile strength (confirmed items / total, source count), top 3 postings by score, top 5 open gap actions by impact (weight x missing coverage), next deadline, next interview, score trend points. Application query + handler, thin controller.

## Frontend

- Denys runs `npx untitledui@latest add sidebar-navigation-base --yes`; use the simple sidebar variant, adapted to React Router, text wordmark, demo user at the bottom.
- `AppShell` layout route wrapping `/dashboard` and placeholder routes. Login "Sign in" navigates to `/dashboard` (`shortcut:` until auth).
- Dashboard built from Untitled UI free pieces (metric cards, badges, table, `charts-base` for the trend). Frontend-design skill for hierarchy.
- "Demo data" label visible.

## Left to the builder

Exact seed content, card layout details, migration name.
