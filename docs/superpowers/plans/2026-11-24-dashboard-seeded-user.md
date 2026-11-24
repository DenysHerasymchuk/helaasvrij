# Dashboard with Seeded Demo User Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `/dashboard` in an Untitled UI sidebar layout, showing one seeded demo student loaded from Postgres through `GET /api/dashboard`.

**Architecture:** Two Domain aggregates: `User` (owns `Source`, `ProfileItem`) and `Posting` (owns `Requirement`, `ScoreSnapshot`). Application has repository interfaces, `ICurrentUser` and a `GetDashboard` handler that computes every aggregate number. Infrastructure has EF configurations, a global `UserId` filter, the repositories, a fixed demo `ICurrentUser` and `DemoSeeder`. The Api controller maps the handler result to Contracts. The React `AppShell` wraps routes with the Untitled UI sidebar.

**Tech Stack:** .NET 10, EF Core 10 + Npgsql, xUnit, NetArchTest; React 19, React Router 8, Tailwind v4, Untitled UI v8 (react-aria-components), recharts via `charts-base`.

**Spec:** `docs/superpowers/specs/2026-11-24-dashboard-seeded-user-design.md`

## Global Constraints

- Score formula: sum(weight x coverage) / sum(weight); Required = 2, Nice = 1; Strong = 1, Partial = 0.5, None = 0. Computed only in `Posting.Score()`.
- Every user-owned table has `UserId` and an EF global query filter `e.UserId == currentUser.UserId`.
- Every `ProfileItem` has a non-empty `SourceRef`; the factory throws otherwise.
- Demo persona is synthetic. Postings seeded with `IsDemo = true`; UI shows "Demo data".
- Shortcuts get a `// shortcut: <limit>, <when to upgrade>` comment.
- Schema changes only via EF migration (`dotnet ef migrations add ... -o Persistence/Migrations`).
- Layer rules in `LayerTests.cs` stay green: Domain has no dependencies; Application does not reference EF or Infrastructure.
- Commit on `develop` after each task, conventional commits, with the attribution trailer.

## Review Focus

- A posting with zero requirements: `Score()` returns 0, not NaN or a divide error. Test in Task 1.
- A user with zero postings: dashboard returns empty lists and null dates; UI shows empty states. Tests in Task 2 and Task 5.
- All deadlines in the past: `NextDeadline` is the earliest future deadline, otherwise null. Test in Task 2.
- Seeder runs twice (container restart): no duplicate user. Check in Task 3.
- API unreachable: the dashboard shows an error message with a retry, not a blank screen. Task 5.

---

### Task 1: Domain model and score

**Files:**
- Create: `backend/src/InternshipCoach.Domain/Users/{User.cs,Source.cs,ProfileItem.cs,SourceKind.cs,ProfileItemKind.cs}`
- Create: `backend/src/InternshipCoach.Domain/Postings/{Posting.cs,Requirement.cs,ScoreSnapshot.cs,Importance.cs,Coverage.cs}`
- Test: `backend/tests/InternshipCoach.Domain.Tests/Postings/PostingScoreTests.cs`, `backend/tests/InternshipCoach.Domain.Tests/Users/ProfileItemTests.cs`

**Interfaces:**
- Produces:
  - `enum SourceKind { GitHub, Cv, Site }`, `enum ProfileItemKind { Skill, Project, Experience, Language }`, `enum Importance { Required, Nice }`, `enum Coverage { None, Partial, Strong }`
  - `User.Create(string gitHubLogin, string displayName, string? avatarUrl, string school, DateOnly stageStart, DateOnly stageEnd) : User`; props `Guid Id`, matching getters, `IReadOnlyList<Source> Sources`, `IReadOnlyList<ProfileItem> ProfileItems`; `AddSource(SourceKind kind, string label, DateTimeOffset? lastSyncedAt)`, `AddProfileItem(ProfileItemKind kind, string name, string sourceRef, string? quote, bool confirmed)`
  - `Source` / `ProfileItem`: `Guid Id`, `Guid UserId`, fields from the spec
  - `Posting.Create(Guid userId, string company, string title, string language, DateOnly? deadline, DateTimeOffset? interviewAt, bool isDemo) : Posting`; `IReadOnlyList<Requirement> Requirements`, `IReadOnlyList<ScoreSnapshot> Snapshots`; `AddRequirement(string text, Importance importance, Coverage coverage, string? evidenceQuote, string? evidenceSource, string? action, string? actionProject, DateOnly? dueDate)`, `AddSnapshot(decimal score, DateTimeOffset takenAt)`, `decimal Score()`
  - `Requirement`: fields from spec plus `bool Done`, `static decimal Weight(Importance)`, `static decimal Value(Coverage)`, `decimal Impact()` = Weight x (1 - Value); 0 when `Done`
  - Private parameterless constructors for EF; backing `List<T>` fields.

- [ ] **Step 1: Write failing tests**

```csharp
[Fact] public void Score_uses_fixed_weights()
{
    var p = Posting.Create(Guid.NewGuid(), "Acme", "Intern", "en", null, null, true);
    p.AddRequirement("C#", Importance.Required, Coverage.Strong, null, null, null, null, null);   // 2 x 1
    p.AddRequirement("Docker", Importance.Required, Coverage.Partial, null, null, null, null, null); // 2 x 0.5
    p.AddRequirement("K8s", Importance.Nice, Coverage.None, null, null, null, null, null);        // 1 x 0
    Assert.Equal(0.6m, p.Score()); // 3 / 5
}
[Fact] public void Score_is_zero_without_requirements() =>
    Assert.Equal(0m, Posting.Create(Guid.NewGuid(), "Acme", "Intern", "en", null, null, true).Score());
[Fact] public void Impact_is_weight_times_missing_coverage() { /* Required+Partial => 1.0m, Nice+None => 1.0m, Required+Strong => 0m */ }
[Fact] public void ProfileItem_without_source_throws() =>
    Assert.Throws<ArgumentException>(() => user.AddProfileItem(ProfileItemKind.Skill, "C#", " ", null, true));
```

- [ ] **Step 2:** `dotnet test backend/tests/InternshipCoach.Domain.Tests` → FAIL (types missing).
- [ ] **Step 3:** Implement the types above. Round `Score()` to 2 decimals.
- [ ] **Step 4:** `dotnet test backend/InternshipCoach.slnx` → all PASS (layer tests included).
- [ ] **Step 5:** Commit `feat(domain): add user and posting aggregates with score`.

### Task 2: Application dashboard query

**Files:**
- Create: `backend/src/InternshipCoach.Application/Abstractions/{ICurrentUser.cs,IUserRepository.cs,IPostingRepository.cs}`
- Create: `backend/src/InternshipCoach.Application/Dashboard/GetDashboard/{GetDashboardHandler.cs,DashboardResult.cs}`
- Modify: `backend/src/InternshipCoach.Application/DependencyInjection.cs` (register handler as scoped)
- Test: `backend/tests/InternshipCoach.Application.Tests/Dashboard/GetDashboardHandlerTests.cs`, fakes in `TestDoubles/`

**Interfaces:**
- Consumes: Task 1 types; existing `IClock`.
- Produces:
  - `interface ICurrentUser { Guid UserId { get; } }`
  - `interface IUserRepository { Task<User?> GetCurrentAsync(CancellationToken ct); }` (loads Sources + ProfileItems)
  - `interface IPostingRepository { Task<IReadOnlyList<Posting>> ListAsync(CancellationToken ct); }` (loads Requirements + Snapshots)
  - `GetDashboardHandler.HandleAsync(CancellationToken ct) : Task<DashboardResult?>` (null when no user)
  - `record DashboardResult(DashboardUser User, int ProfileStrengthPercent, int ConfirmedItems, int TotalItems, int SourceCount, IReadOnlyList<PostingSummary> TopPostings, IReadOnlyList<GapAction> TopActions, DeadlineItem? NextDeadline, DeadlineItem? NextInterview, IReadOnlyList<TrendPoint> Trend)`
  - `record DashboardUser(string DisplayName, string? AvatarUrl, string School, DateOnly StageStart, DateOnly StageEnd)`
  - `record PostingSummary(Guid Id, string Company, string Title, string Language, decimal Score, DateOnly? Deadline, bool IsDemo)`
  - `record GapAction(Guid RequirementId, string Company, string Requirement, string Action, string ActionProject, decimal Impact, DateOnly? DueDate)`
  - `record DeadlineItem(Guid PostingId, string Company, string Title, DateTimeOffset At)`
  - `record TrendPoint(DateTimeOffset At, decimal Score)`

Rules: strength = round(confirmed / total x 100), 0 when no items. Top 3 postings by `Score()` desc. Top 5 actions: requirements with `Action != null`, `Impact() > 0`, sorted Impact desc then DueDate asc. NextDeadline: earliest `Deadline` >= today (`IClock`), else null. NextInterview: earliest `InterviewAt` >= now, else null. Trend: snapshots of the top posting by `TakenAt`, plus its current score at `now`.

- [ ] **Step 1: Write failing tests** with `FakeClock`, `FakeCurrentUser`, `FakeUserRepository`, `FakePostingRepository`:
  - `Returns_null_when_no_user`
  - `Empty_user_gives_empty_lists_and_null_dates` (strength 0, lists empty, both dates null)
  - `Top_actions_sorted_by_impact_then_due_date_and_skip_done`
  - `Next_deadline_ignores_past_deadlines` (one past, one future → future; only past → null)
  - `Top_postings_ordered_by_score_max_three`
- [ ] **Step 2:** `dotnet test backend/tests/InternshipCoach.Application.Tests` → FAIL.
- [ ] **Step 3:** Implement the handler and records.
- [ ] **Step 4:** `dotnet test backend/InternshipCoach.slnx` → PASS.
- [ ] **Step 5:** Commit `feat(application): add dashboard query`.

### Task 3: Persistence, user filter, seeder, migration

**Files:**
- Create: `backend/src/InternshipCoach.Infrastructure/Persistence/Configurations/{UserConfiguration.cs,PostingConfiguration.cs}`
- Create: `backend/src/InternshipCoach.Infrastructure/Persistence/Repositories/{UserRepository.cs,PostingRepository.cs}`
- Create: `backend/src/InternshipCoach.Infrastructure/Identity/DemoCurrentUser.cs`
- Create: `backend/src/InternshipCoach.Infrastructure/Persistence/DemoSeeder.cs`
- Create: `backend/src/InternshipCoach.Infrastructure/Persistence/Migrations/*` (generated, name `InitialDemoModel`)
- Modify: `InternshipCoachDbContext.cs` (DbSets, inject `ICurrentUser`, query filters), `Infrastructure/DependencyInjection.cs` (register repos, `ICurrentUser` scoped, `DemoSeeder`), `Api/Program.cs` (`--migrate` then `DemoSeeder.SeedAsync`), `docker-compose.yml` (api `command`/entrypoint runs `--migrate` before start)

**Interfaces:**
- Consumes: Task 1 types, Task 2 abstractions.
- Produces: `DemoCurrentUser.DemoUserId` = `Guid.Parse("00000000-0000-0000-0000-000000000001")`; `DemoSeeder.SeedAsync(CancellationToken ct) : Task`.

Notes: query filters: `User` by `Id == current.UserId`; `Source`, `ProfileItem`, `Posting` by `UserId`; `Requirement` and `ScoreSnapshot` reachable only through `Posting` (owned collections or required navigation; `UserId` column also on them if EF needs a direct filter). Enums stored as strings. `User.Create` gets an overload or internal setter so the seeder can use `DemoUserId`. Seeder: skip when `IgnoreQueryFilters().Any(u => u.Id == DemoUserId)`. Seed content: synthetic student "Lotte Peeters", Thomas More, stage 2027-02-01 to 2027-05-31; sources GitHub `lotte-dev` (repos `weather-app`, `budget-buddy`, `kotlin-quiz`), CV; ~10 profile items each with `SourceRef` like `github:weather-app/README.md`; 4 demo postings (NL, FR, EN, EN) with 4 to 6 requirements each, actions naming those repos, deadlines in the future (relative to `IClock.UtcNow`), one `InterviewAt`; 3 snapshots on the strongest posting rising to its current score. Comment `// shortcut: fixed demo user, replace with GitHub OAuth claim` in `DemoCurrentUser`. Compose: `entrypoint: ["sh","-c","dotnet InternshipCoach.Api.dll --migrate && dotnet InternshipCoach.Api.dll"]`.

- [ ] **Step 1:** Write configurations, filters, repos, current user, seeder; `dotnet build backend/InternshipCoach.slnx` → 0 errors.
- [ ] **Step 2:** `dotnet ef migrations add InitialDemoModel -p backend/src/InternshipCoach.Infrastructure -s backend/src/InternshipCoach.Api -o Persistence/Migrations` → migration files created.
- [ ] **Step 3:** User runs `docker compose up --build api db` twice; then `docker compose exec db psql -U coach -d coach -c 'select count(*) from coach."Users"'` → `1` both times.
- [ ] **Step 4:** `dotnet test backend/InternshipCoach.slnx` → PASS.
- [ ] **Step 5:** Commit `feat(infrastructure): persist demo model with user filter and seeder`.

### Task 4: Contracts and API endpoint

**Files:**
- Create: `backend/src/InternshipCoach.Contracts/Dashboard/DashboardResponse.cs` (records mirroring Task 2 records, same names with `Response` suffix where they collide)
- Create: `backend/src/InternshipCoach.Api/Controllers/DashboardController.cs`

**Interfaces:**
- Consumes: `GetDashboardHandler.HandleAsync`.
- Produces: `GET /api/dashboard` → 200 `DashboardResponse` (camelCase JSON), 404 when handler returns null. Frontend Task 5 relies on these JSON fields: `user{displayName,avatarUrl,school,stageStart,stageEnd}`, `profileStrengthPercent`, `confirmedItems`, `totalItems`, `sourceCount`, `topPostings[{id,company,title,language,score,deadline,isDemo}]`, `topActions[{requirementId,company,requirement,action,actionProject,impact,dueDate}]`, `nextDeadline{postingId,company,title,at}|null`, `nextInterview{...}|null`, `trend[{at,score}]`.

- [ ] **Step 1:** Implement controller and mapping.
- [ ] **Step 2:** With the stack up: `curl -s localhost:5080/api/dashboard | jq '.user.displayName, (.topPostings|length), (.topActions|length)'` → `"Lotte Peeters"`, `3`, `5`.
- [ ] **Step 3:** Commit `feat(api): add dashboard endpoint`.

### Task 5: Frontend app shell and dashboard

**Prerequisite:** user has run `cd frontend && npx untitledui@latest add sidebar-navigation-base charts-base --yes`. If not present, stop and ask.

**Files:**
- Create: `frontend/src/core/app-shell.tsx` (sidebar layout + `<Outlet/>`)
- Create: `frontend/src/core/api.ts` (`apiGet<T>(path: string): Promise<T>`; throws on non-2xx)
- Create: `frontend/src/features/dashboard/{dashboard-page.tsx,types.ts}`
- Create: `frontend/src/features/placeholder/coming-soon-page.tsx`
- Modify: `frontend/src/main.tsx` (layout route `AppShell` with `/dashboard`, `/postings`, `/gaps`, `/documents`, `/interview`, `/applications`, `/profile`, `/settings`), `frontend/src/features/auth/login-page.tsx` (Sign in → `navigate("/dashboard")`, `// shortcut: no auth yet, replace with real sign-in`)

**Interfaces:**
- Consumes: Task 4 JSON.
- Produces: `types.ts` `DashboardResponse` TS type matching Task 4 fields.

Notes: Load the `frontend-design` skill first. Use the simple sidebar variant from `sidebar-navigation-base`, items in spec order with Untitled UI icons, Settings in the footer, demo user name/avatar/school at the bottom, active item from React Router location. Text wordmark "Internship Coach". Dashboard sections: header (greeting, stage period, "Demo data" badge), profile strength + sources, top postings (score as %, language badge, deadline), top gap actions (action, project, impact, due date), next deadline/interview, score trend line (`charts-base`). States: loading, error with "Try again" button that refetches, empty states per section ("No postings yet. Add one from Postings.").

- [ ] **Step 1:** Implement the shell, routes and dashboard.
- [ ] **Step 2:** `cd frontend && npm run build` → 0 errors.
- [ ] **Step 3:** `npm run dev` with the API up; open `http://localhost:5173/dashboard`: sidebar renders, all numbers come from the API, active item highlighted, layout works at 375px width.
- [ ] **Step 4:** Stop the API; reload → error message with "Try again".
- [ ] **Step 5:** Commit `feat(frontend): add app shell with sidebar and dashboard`.

### Task 6: Full stack check and docs

**Files:**
- Modify: `CLAUDE.md` (layout section: new folders), `FEATURES.md` (mark sidebar/dashboard rows)

- [ ] **Step 1:** User runs `docker compose up --build`; `curl -s localhost:5173/api/dashboard | jq .user.displayName` → `"Lotte Peeters"`; `http://localhost:5173/dashboard` renders.
- [ ] **Step 2:** Commit `docs: update layout and feature status`; push `develop`.
