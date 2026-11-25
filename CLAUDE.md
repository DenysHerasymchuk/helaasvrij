# helaasvrij (repo `internship-coach`)

"The internship coach that tells you what's missing." A private dashboard that helps people in Belgium land a position (job, internship or anything between). The search runs like a Jira project: every position sits in the **backlog**; a **sprint** is one round of applications (started and completed by hand, one at a time); the **board** shows the running sprint (Saved, Applied, Interview, Offer, Rejected); finished sprints keep a report; every position has an **activity** log and a key like `BRI-12`.

Built in a 6-hour timebox with AI coding agents, then extended. Brand: lowercase "helaasvrij", Fraunces serif, Paper #F6F1E7, Ink #1B1B1B, Stamp #1F7A4D, Highlight #FFE45C, Gap #D93A2B, Muted #8A8578. Logo in `frontend/src/core/logo.tsx` and `frontend/public/brand/`.

## Principles (do not break)

- **The gap plan is the product; the CV is a by-product.** Value is gap analysis citing the user's own evidence.
- **Every claim has a source** (repo, file, CV line, site). Claims without one are dropped. Drafts use only confirmed items. Never invent or inflate.
- **Actions are specific.** Each names one of the user's own projects; reject and regenerate otherwise. No repos: a small new kebab-case project instead.
- **The score is computed in code, not by the LLM.** sum(weight x coverage) / sum(weight); required 2, nice 1; strong 1, partial 0.5, none 0.
- **Private by default.** Every user-owned table has `UserId` and an EF Core global query filter. "Delete my data" (`AccountDeleter`) walks every `IUserOwned` type, so a new owned table needs a seeded row in `AccountDeleterTests`.
- **Belgian context.** Postings and outputs in NL, FR or EN (the posting's language), Belgian dates.
- No LinkedIn scraping (`PostingUrlPolicy` blocks linkedin.com, lnkd.in, student.be, jobteaser.com; the bookmarklet sends pages from the user's own browser). No auto-sending messages. No employer side. No role without a posting.

## Pages

Home `/dashboard`, Gap plan `/gaps`, Documents `/documents`, Interview prep `/interview`, Board (tabs `/summary`, `/list`, `/board`, `/backlog`, `/sprints`), Calendar `/calendar` (month and week, day list), Find positions `/find`, Position `/positions/:id`, Settings `/settings` (profile, preferences, account), Connect Claude `/connect-claude`. `/` redirects to `/login`.

## Stack and layout

- Frontend: React, React Router, Tailwind v4, Vite, TypeScript, Untitled UI (`@/` = `src`). Build UI from the `untitledui` MCP components first; load the `frontend-design` skill before reshaping a page.
- Backend: ASP.NET Core 10, Clean Architecture + DDD, EF Core + PostgreSQL schema `coach`, migrations only. Dependencies point inward (`InternshipCoach.Architecture.Tests`): Domain, Application (Command + Handler per feature), Infrastructure, Contracts, Api.
- Auth: cookie session (email or GitHub OAuth) plus `Bearer` personal access tokens (`hv_...`, hashed, from Settings > Account) for MCP. Tokens cannot manage tokens.
- LLM: route33 (`https://api.route33.ai`, base URL without `/v1`), optional Claude Code CLI fallback (installed in the API image). DeepSeek for cheap work, Sonnet 5.5 for gap matrix and documents. Validate JSON; retry once. Budget about EUR 50: never loop LLM calls without a cap.
- Assistant and MCP: one tool catalog (`ChatToolbox`, `PlanningTools`, `GET /api/tools`) serves the chat widget and `mcp/` (stdio server). Action tools only propose; they run after `POST /api/chat/actions/confirm`. Tool output is wrapped in `<untrusted_data>`.
- External: GitHub REST, Adzuna (`be`), optional Zapier webhook.
- Infra: Docker Compose (`db` postgres 17, `api` :5080, `web` :5173).

## Commands

- Run all: `cp .env.example .env`, set the password, `docker compose up --build`
- Backend: `dotnet build backend/InternshipCoach.slnx`, `dotnet test backend/InternshipCoach.slnx`
- Migration: `dotnet ef migrations add <Name> -p backend/src/InternshipCoach.Infrastructure -s backend/src/InternshipCoach.Api -o Persistence/Migrations` (set `ConnectionStrings__InternshipCoach` to any value)
- Frontend: `cd frontend && npm run dev`, `npm run build` (the build is stricter than `tsc --noEmit`: unused code fails it)
- MCP: `cd mcp && npm install && npm test`
- Navigation: `graphify extract . --code-only`, then `graphify query "<question>"`
- Branches: `main` and `develop`; work on `develop`. Commits carry no co-author or attribution lines.

## Secrets

`R33_API_KEY`, `GITHUB_CLIENT_ID`, `GITHUB_CLIENT_SECRET`, `ADZUNA_APP_ID`, `ADZUNA_APP_KEY`, `WEBHOOK_SECRET`, database password. Keep them in `.env` (git-ignored); commit only `.env.example`. Never log keys or CV content.
