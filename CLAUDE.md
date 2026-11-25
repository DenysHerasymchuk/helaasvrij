# Internship Coach (working name)

Hack The Future (Belgium) project, built in a 6-hour timebox with AI coding agents. Final product name is not decided yet (shortlist: StagiAIr, DeSync, Gatenkaas, Geen Zever, Brugske).

Branding for now: the name is "helaasvrij" (lowercase; tagline "The internship coach that tells you what's missing."). Logo: a Fraunces serif "h" (mark) or "helaasvrij" (wordmark) in Ink with a hand-drawn Gap-red strike-through; `frontend/src/core/logo.tsx` in the app, SVGs in `frontend/public/brand/`, favicon is the struck "h" on a Paper rounded square. Colours: Paper #F6F1E7, Ink #1B1B1B, Stamp #1F7A4D, Highlight #FFE45C, Gap #D93A2B, Muted #8A8578. Repository: `internship-coach` (private, GitHub).

## The idea

A private, personal dashboard that helps people in Belgium land a position (job, internship or anything in between).

The user connects GitHub, uploads a CV or LinkedIn PDF and adds a personal website. The app builds an evidence-backed profile, matches it against real Belgian postings, and turns every gap into a concrete action tied to the user's own projects. From the same data it writes the CV, cover letter, recruiter message and interview prep.

The search is run like a Jira project: every position you consider sits in the **backlog**; a **sprint** is one round of applications (name, goal, dates, started and completed by hand, one running at a time); the **board** shows the running sprint as columns (Saved, Applied, Interview, Offer, Rejected); finished sprints stay in the **sprint history** with a report; every position has an **activity** log. Positions get a key such as `BRI-12` (first three letters of the company plus a per-user running number).

Pitch: "Drop in your links, see exactly what each Belgian position wants from you, close the gaps, and apply with a CV, cover letter and interview prep built from your real work."

## Core principles (do not break these)

- **The gap plan is the product; the CV is a by-product.** Generic CV generation is a commodity (ChatGPT, Teal, Kickresume). The value is gap analysis that cites the student's own evidence.
- **Every claim has a source.** Every extracted skill, project or experience stores where it came from (repo, file, CV line, site). Claims without a source are dropped. The CV and drafts use only items the student confirmed. Never invent or inflate.
- **Actions must be specific.** Every gap action names one of the student's own projects ("Add a Dockerfile to `weather-app`"). Reject and regenerate any action that does not. A student without GitHub repos gets a small new project instead ("Start `expense-tracker-api`: ..."), with a kebab-case name and the requirement's skill in the action.
- **The match score is computed in code, not by the LLM.** The LLM labels each requirement; code computes the number so the same input always gives the same score.
- **Private by default.** Each student sees only their own data.
- **Belgian context.** Postings and outputs in NL, FR or EN (output in the posting's language), Belgian dates and languages.

## Privacy and data ownership

- Sign-in with GitHub OAuth. No anonymous access to any page except login and register.
- Every user-owned table has a `UserId`. Every query and endpoint filters by the signed-in user. Never return another user's data; enforce this in the data layer (EF Core global query filter on `UserId`), not only in controllers.
- Shared data is limited to: anonymous `CompanyQuestions` (interview questions per company) and public postings that arrive through the webhook, copied into a user's list only when matched.
- "Delete my data" removes every row owned by the user.
- Never scrape LinkedIn. LinkedIn data comes only from the user's own "Save to PDF" export.

## Pages (sidebar navigation)

| Sidebar item | Route | Content |
| --- | --- | --- |
| Home | `/dashboard` | Running sprint (goal, days left, progress) or a prompt to start one, source status, top matched postings, top gap actions, next deadline and interview, applications per month |
| Gap plan | `/gaps` | All gaps across postings as one checklist, sorted by impact; ticking re-scans and updates scores; closed gaps feed cover letters |
| Documents | `/documents` | CV (NL/FR/EN, print to PDF), cover letters and recruiter messages (max 300 chars, copy only) per posting |
| Interview prep | `/interview` | Questions from gaps, from the student's repos, and shared company questions; practice answers with feedback |
| Backlog | `/backlog`, `/backlog/find` | Running sprint on top, every other position below; select rows and add them to the sprint; start a sprint; add by paste or URL; "Find positions" tab searches Adzuna |
| Board | `/board` | Kanban of the running sprint (Saved, Applied, Interview, Offer, Rejected) with the sprint goal, progress and "Complete sprint" (open positions go to the next sprint or back to the backlog) |
| Sprints | `/sprints`, `/sprints/:id` | History of all sprints; report per sprint (applied, interviews, offers, rejected, average match, gaps closed) |
| Position | `/positions/:id` | One position: key, gap matrix, requirements, dates, activity log |
| Profile | `/profile` | Sources (GitHub, CV/LinkedIn PDF, site, paste-text fallback) and extracted items with source links (remove only; items count at once) |
| Settings | `/settings` | Email alerts and strong-match threshold, default language, delete all my data, sign out |

No landing page: `/` redirects to `/login`.

## Stack

- **Frontend:** React + React Router + Tailwind CSS (Vite, TypeScript).
- **Backend:** ASP.NET Core (.NET) Web API.
- **Data:** EF Core with PostgreSQL. Schema changes only through EF Core migrations.
- **Auth:** GitHub OAuth in ASP.NET Core, cookie session shared with the frontend.
- **LLM:** route33 gateway (`https://api.route33.ai`, Anthropic-compatible; base URL without `/v1`). Use the official Anthropic C# SDK with a custom base URL.
  - `deepseek/deepseek-v4.1-flash-1017` for cheap work: profile extraction, language detection.
  - `anthropic/claude-sonnet-5-5` for gap matrix, CV, cover letter, recruiter message, interview prep.
  - Ask for JSON and validate it against a schema; if structured output is not passed through by route33, put the schema in the prompt, parse, validate and retry once.
  - Budget is about EUR 50 per key. Keep prompts lean; never loop LLM calls without a cap.
- **External APIs:** GitHub REST API (with the user's OAuth token), Adzuna jobs API (country `be`), optional Zapier webhook for job-alert emails.
- **Infra:** Docker + Docker Compose (services: `web`, `api`, `db`). Git for version control.

## UI: Untitled UI + design skill

- **Build the UI mostly from the Untitled UI MCP server** (`untitledui`). Before writing any component or page by hand, look for an existing one:
  - `get_page_templates` / `get_page_template_files` for whole pages (dashboard, settings, sidebar layouts).
  - `search_components` / `list_components`, then `get_component` or `get_component_bundle` for sidebar, cards, tables, tabs, badges, checklists, kanban, file upload, empty states, modals.
  - `search_icons` for icons.
- Adapt the fetched components to this project (React Router links, our data, Tailwind tokens); do not rebuild what Untitled UI already provides. Hand-write a component only when nothing fits, and match the Untitled UI style when you do.
- **Load the `frontend-design` skill before building or reshaping any page or layout**, and follow it for visual direction, typography, spacing and hierarchy. The result must not look like a default template.
- Keep one consistent design system: shared colour and spacing tokens in the Tailwind config, light and dark mode, responsive down to phone width, accessible (labels, focus states, contrast).

## Layout

Backend follows Clean Architecture + DDD, modelled on `mcloots/2627_SWE_ITF`. Dependencies point inward only (enforced by `InternshipCoach.Architecture.Tests`):

```
backend/
  InternshipCoach.slnx, Dockerfile
  src/
    InternshipCoach.Domain/          entities + self-validating value objects, factory methods, no dependencies
    InternshipCoach.Application/     use cases per feature folder (Command + Handler), abstractions (IClock, repositories)
    InternshipCoach.Infrastructure/  EF Core DbContext (schema "coach"), configurations, migrations, repositories, SystemClock
    InternshipCoach.Contracts/       API request/response records only
    InternshipCoach.Api/             thin controllers, Program.cs (env prefix COACH_, Swagger in dev, --migrate)
  tests/                             Domain.Tests, Application.Tests (fakes in TestDoubles/), Architecture.Tests
frontend/                            React + Vite + TS + Tailwind + React Router, `@/` = src
  src/features/<feature>/            pages per feature (auth: login, register)
  Dockerfile, nginx.conf             nginx serves the build and proxies /api to the api service
docker-compose.yml                   db (postgres:17), api (:5080), web (:5173)
.env.example                         POSTGRES_PASSWORD; copy to .env
```

New feature in the backend: Domain entity/value objects, then Application `Feature/Action/{Command,Handler}`, register the handler in `AddApplication`, repository interface in Application + implementation in Infrastructure, request/response in Contracts, controller in Api.

## Commands

- Backend: `dotnet build backend/InternshipCoach.slnx`, `dotnet test backend/InternshipCoach.slnx`
- Migration: `dotnet ef migrations add <Name> -p backend/src/InternshipCoach.Infrastructure -s backend/src/InternshipCoach.Api -o Persistence/Migrations`
- Frontend: `cd frontend && npm run dev` (proxies /api to localhost:5080), `npm run build`
- Everything: `cp .env.example .env`, set the password, `docker compose up --build`
- Navigation: `graphify extract . --code-only` then `graphify query "<question>"` (output in git-ignored `graphify-out/`)
- Branches: `main` and `develop`; work on `develop`.

## Core flow (build this first)

1. Ingest: GitHub repos (languages, README, file tree: Dockerfile, tests, CI; commit count), CV/LinkedIn PDF text, site text.
2. Extract profile: DeepSeek turns raw sources into skills, projects, experience, languages, each with a source reference.
3. Gap matrix against a posting: per requirement — importance (required/nice), coverage (strong/partial/none), evidence quote and source, action, action project.
4. Score: sum(weight x coverage) / sum(weight); required = 2, nice = 1; strong = 1, partial = 0.5, none = 0.
5. Re-scan: when a gap is ticked, re-ingest that repo and re-check that requirement.

Nothing else is built until this flow works end to end.

## Build order

1. Scaffold frontend, backend, compose, GitHub sign-in, LLM client; test a JSON call through route33.
2. Ingest, profile extraction, gap matrix, score.
3. Dashboard, postings detail, gap re-scan.
4. Adzuna postings, documents (CV, cover letter, recruiter message), interview prep.
5. Profile page, Belgian layer, delete my data, cache the demo path.
6. Webhook/Zapier, applications board — only if the core is solid.

## Non-goals

- No employer side, no public job board, no browsing other students.
- No LinkedIn scraping, no auto-sending messages.
- No "role type without a posting" mode (it produces generic advice).

## Secrets

Required: `R33_API_KEY`, `GITHUB_CLIENT_ID`, `GITHUB_CLIENT_SECRET`, `ADZUNA_APP_ID`, `ADZUNA_APP_KEY`, `WEBHOOK_SECRET`, database password. Keep them in `.env` (git-ignored); commit only `.env.example`. Never log keys or user CV content.

## Demo (90 seconds)

Sign in, sources fill the profile with evidence, a judge's company posting appears in the list, open its gap matrix next to plain ChatGPT's generic answer, tick a gap and re-scan (score rises, e.g. 62% to 81%), download the CV in Dutch, show tailored interview questions.
