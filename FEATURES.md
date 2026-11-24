# Features

All features for Internship Coach, grouped by build priority. `CLAUDE.md` holds the principles and the stack. This file is the checklist.

Status: `done`, `todo`, or `next` (not in the hackathon build; goes on the "next" slide).

## P0: core flow (nothing else until this works end to end)

| # | Feature | Technical notes | Status |
| --- | --- | --- | --- |
| 1 | Scaffold: backend, frontend, compose, `/api/health` | .NET Clean Architecture, Vite + React, Postgres, nginx proxy | done |
| 2 | Login and register pages | Untitled UI templates, placeholders for now | done |
| 3 | route33 LLM client | Anthropic C# SDK, custom base URL, Bearer auth. Test one call first (untested from .NET). `max_tokens` >= 4000. Retry once on empty text or invalid JSON. No forced `tool_choice` (route33 rejects it). Schema goes in the prompt. | done (real smoke test skipped until R33_API_KEY is set) |
| 4 | Token and cost log per run | Log input/output tokens and model per LLM call, never the prompt content. EUR 50 cap, route33 prices unknown. | done (table `LlmCalls`; per-user daily cap, default 200) |
| 5 | Start without sign-up | Enter a GitHub username and paste CV text, then get a result. No account wall before the first result. | todo (needs anonymous sessions that conflict with the per-user query filter; not done to keep auth intact) |
| 6 | GitHub ingest | REST API with a server token (unauthenticated limit is 60/h). Repos, languages, README, file tree (Dockerfile, tests, CI), commit count. | done (`/api/sources/github`; up to 10 non-fork repos, inline sync; needs `COACH_GitHub__Token` for the 5,000/h limit) |
| 7 | CV ingest | Paste text first; PDF upload (CV or LinkedIn "Save to PDF") after. | done (`/api/sources/cv` PDF via PdfPig, `/api/sources/text` paste, `/api/sources/site` website with SSRF guard; no UI until the Profile page) |
| 8 | Profile extraction | LLM turns sources into skills, projects, experience, languages. Each item stores its source (repo, file, CV line). Items without a source are dropped. | done (`POST /api/profile/extract`; Smart model for CV, site and pasted text, Cheap only for GitHub summaries; every quote is checked against its document and drops are counted in the response) |
| 9 | Verbatim-quote check | Code checks that every evidence quote is an exact substring of its source text. Drop or retry on failure. Count failures (target 0). In tests, GLM spliced quotes in 3 of 12 runs; this check caught every one. | done (`QuoteCheck`; failure counting is wired in by profile extraction) |
| 10 | Confirm/remove profile items | Student confirms or removes each extracted item. Drafts use only confirmed items. | done (`PATCH`/`DELETE /api/profile/items/{id}`, `POST /api/profile/items/confirm-all`; items cascade away when their source is removed or re-synced) |
| 11 | Paste a posting | Paste text (or one of the demo postings). Detect language (NL/FR/EN). | done (`POST /api/postings/paste`, Cheap model via `UntrustedInput.Wrap`; 4 demo postings seeded for the dev user) |
| 12 | Gap matrix | Per requirement: importance (required/nice), coverage (strong/partial/none), evidence quote + source, action, action project. | done (`POST /api/postings/{id}/analyse`, Smart model, only confirmed items; matrix on the posting detail page; `RecheckRequirementHandler` is ready for the re-scan, no endpoint yet) |
| 13 | Action must name a project | Code rejects any action whose project is not one of the student's repos, then regenerates once. | done (project must be one of the GitHub repo names and appear in the action; one regeneration call, then the action stays empty; hard cap 2 LLM calls per analyse) |
| 14 | Score in code | sum(weight x coverage) / sum(weight); required 2, nice 1; strong 1, partial 0.5, none 0. | done (`Posting.Score()` in Domain, whole-percent rounding, zero requirements = 0; `ScoreSnapshot` stored on every analyse) |
| 15 | Formula shown on the report | Show the weights and the formula next to the score. No unexplained number. | done (formula and weights next to the score ring) |
| 16 | "No clear fit" state | Honest message when the score is low or the requirements barely overlap. | done (`Posting.NoClearFit()`: below 40% or fewer than half the required items at least Partial) |
| 17 | Dated gap plan | Gaps as a checklist sorted by impact (weight x missing coverage), each with a target date. | done (`GET /api/gaps`, `/gaps` page with open/done and posting filters, grouped by posting or repo) |
| 18 | Re-scan | Tick a gap, re-ingest that repo, re-check that requirement, update the score (demo: 62% to 81%). | done (`POST /api/gaps/{id}/done` and `/undo`; merges only that repo into the GitHub source so confirmed items and evidence survive, reads the changed documents, new items stay unconfirmed, one re-scan per requirement per minute held in memory; returns old and new score; not run live against GitHub or route33) |
| 19 | Prompt-injection safety | Postings, CVs and READMEs are data only. The model gets no tools. Strict schema validation with one retry. | done in the client (delimiters, no tools, schema validation, one retry); each feature must use `UntrustedInput.Wrap` |

## P1: after the core works (freeze scope at 15:00)

| # | Feature | Technical notes | Status |
| --- | --- | --- | --- |
| 20 | Accounts: email + password and GitHub OAuth sign-in | Cookie session (HttpOnly, SameSite=Lax), users table, `/api/auth/*`, rate-limited. No email verification or password reset yet. Optional: P0 works without it. | done |
| 21 | Per-user data isolation | `UserId` on every owned table, EF Core global query filter. Needed once accounts exist. `IUserOwned`, `ICurrentUser`, filter + save guard in `InternshipCoachDbContext`; no owned tables yet. | done |
| 22 | Sidebar layout + dashboard | Profile strength, top postings, top gap actions, score trend. Reads `GET /api/dashboard` through `useDashboard()` (sources, postings with requirements and scores, applications by month). | done |
| 23 | Postings list + detail | Filters (language, score, new); detail shows the gap matrix and score. | partly done (list with search and language filter, detail with requirements, dates, delete; gap matrix and score done in rows 12-16; score filter still todo) |
| 24 | CV draft | Confirmed items only, in the posting's language, print to PDF. | partly done (row 35 tailors a pasted CV with quote checks; a CV built from confirmed profile items is still todo) |
| 25 | Cover letter draft | Uses closed gaps as evidence. | done (`POST /api/documents/draft` kind `coverLetter`; evidence quotes and closed gaps only; not run live against route33) |
| 26 | Recruiter message | Max 300 characters, copy only, never auto-sent. | done (`kind: recruiterMessage`, max 300 chars enforced in the model check and the domain; copy only; not run live) |
| 27 | Demo safety | Cached demo run, recorded backup video, postings labelled "demo data", synthetic or consenting persona. Replay decorator (`COACH_Demo__Replay`, `COACH_Demo__Record`) and `scripts/record-demo.md` done; recording the cached run and the video itself still to do. | todo |
| 28 | Gold set | About 10 hand-labelled cases; report quote-check failures and score variance over 5 runs. Runner `backend/tests/InternshipCoach.Evaluation` with 10 synthetic cases, offline by default, live with `R33_API_KEY`; first real-model run still to do. | done |
| 29 | Side-by-side with ChatGPT | Same posting, plain ChatGPT answer vs our evidence-backed matrix. | todo |
| 30 | Profile page | Sources (GitHub, CV/LinkedIn PDF, site, paste-text fallback), extracted items with source links and confirm/remove, school and stage period. | partly done (`/profile`: sources, items with quotes and source links, confirm/remove, school and stage period with native date inputs; PDF upload and date range use plain controls until the Untitled UI file upload and date range picker are installed) |
| 31 | Belgian layer | Output in the posting's language (NL/FR/EN), CEFR language levels on profile and requirements, stage period checked against the posting's dates. | partly done (cover letter, recruiter message, CV and gap actions follow the posting language NL/FR/EN; CEFR levels are stored on profile items; stage period vs posting dates not checked) |
| 32 | Add posting by URL | Fetch the page, read JobPosting JSON-LD when present, fall back to paste. Never LinkedIn, Student.be or JobTeaser. | done (`SafePageFetcher`: SSRF guard at connect time and on redirects, 10 s timeout, 1 MB cap, one User-Agent) |
| 33 | Settings page (basic) | Default language, sign out. | done (also strong-match threshold, email-alerts toggle stored only, and delete my data via `DELETE /api/me`, generic over every `IUserOwned` table) |
| 34 | AI chat assistant | Round button bottom-right on every signed-in page, opens a chat through route33 (`anthropic/claude-sonnet-5-5`). Answers questions about the app and suggests a page with a "Go to" chip (whitelisted routes only). Later: navigates and acts on the user's data. See `BUILD.md` prompt 10. | done (answers and "Go to" chip; no actions yet, no chat history) |
| 35 | CV tailoring | On `/documents` the student pastes CV text or uploads a PDF/DOCX (max 5 MB, extension + magic bytes checked, text extracted server-side with PdfPig/OpenXml, file never stored) and edits the extracted text before tailoring. Posting: pick a saved posting (`/api/postings`, user-owned, 404 for others) or paste text. Claude (Smart model only) rewrites the CV for that posting in its language (or NL/FR/EN on request). Every bullet and skill carries a verbatim quote from the confirmed CV text checked with `QuoteCheck`; lines with a failed quote or a number not in the CV are dropped and counted. Saved as a `Document` (edit, copy, delete, print to PDF, download .docx via `GET /api/documents/{id}/docx`, which renders the saved edits). | done (not run live against route33 or in Docker) |
| 36 | Interview prep | `GET /api/interview/{postingId}/questions` generates once and stores 3 gap + 3 repo questions (Smart model, every question must name one of the student's repos, max 2 LLM calls) plus shared anonymous `CompanyQuestions` (seeded for the 4 demo companies). `POST /api/interview/answers` gives short feedback (good, add, example sentence), max 5 tries per question. `/interview` page. | done (not run live against route33; plain controls) |
| 37 | Adzuna postings | Shared snapshot of Adzuna ads (3 calls a day: stage, internship, stagiair) at `/postings`; list and detail show company initials logo, location, posted date, salary ("estimate" when Adzuna predicted it), contract, category and a "Jobs by Adzuna" link per ad (Adzuna terms). Saving looks the ad up server-side and never trusts client text. | done (`GET /api/jobs`, `GET /api/jobs/{id}`, `POST /api/postings/from-job`; detail page `/postings/job/:jobId` before saving; snapshot file has `schemaVersion` 2 so an older file is refetched once; a failed fetch waits an hour; Adzuna only shares the first ~500 characters, the full ad opens on adzuna.be; not run live) |
| 38 | Save to applications | Saving an Adzuna ad shows "Saved to your postings" with "View saved postings" and "Open posting"; saving the same ad again (same link, tracking query ignored) returns the existing posting without a model call. `/postings/saved` rows and `/postings/:id` show "Add to applications" (lands in Saved) or "On your board: <stage>" with a link to `/applications`; board cards link back to the posting. | done (`POST /api/postings/from-job` answers 200 with the existing posting; `POST /api/applications` is idempotent per user and posting and returns the existing card instead of 409; stage per posting read from `GET /api/applications`; no migration; not run live, no Docker) |

## Next (pitch slide, not built)

| Feature | Why it waits |
| --- | --- |
| Applications kanban | Built, not part of the core value: `/applications` board (Saved, Applied, Interview, Offer, Rejected) with drag and drop and a "Move to" menu, `GET/POST /api/applications` (POST idempotent per posting), `PATCH /api/applications/{id}`, `GET /api/applications/stats?months=6`. Stats feed the dashboard through `GET /api/dashboard`. Not run against a real database or browser. |
| Adzuna postings | Free tier is evaluation only; Belgium not confirmed |
| Zapier/webhook job alerts | Depends on accounts and postings |
| CV in all three languages at once | One language (the posting's) is enough for the demo |
| Shared company questions | Needs many users |
| Settings: sending email alerts | Preference is stored; nothing sends yet. Delete-my-data is built (row 33) |
| ESCO / O*NET skill normalisation | ESCO misses modern tooling (docker 0 hits) |

## Rules that apply to every feature

- Never scrape LinkedIn, Student.be or JobTeaser. Postings come from paste or verified demo data.
- Real CVs go only to Claude (`anthropic/claude-sonnet-5-5`). No data-processing terms were found for DeepSeek, Moonshot or Z.ai via route33.
- Kimi is slow (about 15 s): keep it out of the live path.
- Claim "evidence-checked", never "accurate". Do not say "first": Career Decoder, ProofMap, Magnet.me, Jobscan and Teal exist.
- Candidate-side only, no ranking for employers, a human approves every draft (EU AI Act position).
