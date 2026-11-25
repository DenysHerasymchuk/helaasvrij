using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;
using InternshipCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

/// <summary>
/// Development only: gives the dev test user (and nobody else) a confirmed demo profile and the four synthetic demo
/// postings. The profile is a GitHub-like source with three repos plus a CV page, twelve confirmed items whose quotes
/// are verbatim from those documents, and so analysis works without connecting real
/// sources. Safe to run again: only missing parts are added (documents, items, basics fields, postings, generic
/// answers, stage dates); nothing the student changed is overwritten.
/// </summary>
public static class DemoDataSeeder
{
    private sealed class FixedUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    public static async Task SeedPostingsAsync(
        DbContextOptions<InternshipCoachDbContext> options, IClock clock, string email, CancellationToken ct)
    {
        await using var lookup = new InternshipCoachDbContext(options);
        if (!Email.TryCreate(email, out var address))
        {
            return;
        }

        var user = await lookup.Users.FirstOrDefaultAsync(u => u.Email == address, ct);
        if (user is null)
        {
            return;
        }

        // Every query below runs under this user's global filter, and every new row gets this user's id.
        await using var db = new InternshipCoachDbContext(options, new FixedUser(user.Id));
        var documents = await SeedSourcesAsync(db, user.Id, clock.UtcNow, ct);
        await SeedProfileAsync(db, user.Id, documents, clock.UtcNow, ct);
        await SeedDemoPostingsAsync(db, user.Id, clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Dictionary<string, Guid>> SeedSourcesAsync(
        InternshipCoachDbContext db, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var sources = await db.Sources.Include(s => s.Documents).ToListAsync(ct);
        var ids = new Dictionary<string, Guid>();
        foreach (var (kind, label, docs) in DemoSources)
        {
            var source = sources.FirstOrDefault(s => s.Kind == kind && s.Label == label);
            if (source is null)
            {
                source = Source.Create(userId, kind, label);
                db.Sources.Add(source);
            }

            foreach (var (path, text, metadata) in docs)
            {
                var document = source.Documents.FirstOrDefault(d => d.Path == path);
                if (document is null)
                {
                    document = source.AddDocument(path, text, metadata);
                    db.Add(document);
                }

                ids[path] = document.Id;
            }

            if (source.Status != SourceStatus.Ready)
            {
                source.MarkReady(now);
            }
        }

        return ids;
    }

    private static async Task SeedProfileAsync(
        InternshipCoachDbContext db, Guid userId, Dictionary<string, Guid> documents, DateTimeOffset now, CancellationToken ct)
    {
        var items = await db.ProfileItems.ToListAsync(ct);
        foreach (var (kind, name, path, quote) in DemoItems)
        {
            if (items.Any(i => i.Kind == kind && i.NormalizedName == ProfileItem.Normalise(name)))
            {
                continue;
            }

            var item = ProfileItem.Create(userId, kind, name, documents[path], quote, new CreatedAt(now));
            item.SetConfirmed(true);
            db.ProfileItems.Add(item);
        }
    }

    private static async Task SeedDemoPostingsAsync(InternshipCoachDbContext db, Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.Postings.Where(p => p.IsDemo).ToListAsync(ct);
        foreach (var demo in Demos)
        {
            var posting = existing.FirstOrDefault(p => p.Company == demo.Company);
            if (posting is null)
            {
                posting = Posting.Create(userId, demo.Company, demo.Title, demo.City, demo.Language, null, demo.Text,
                    demo.Deadline, demo.Requirements, isDemo: true, new CreatedAt(now));
                db.Postings.Add(posting);
            }

            if (posting.GenericAnswer is null)
            {
                posting.SetGenericAnswer(demo.GenericAnswer, GenericAnswerCapturedAt);
            }
        }
    }

    private const string BudgetBuddyReadme =
        "# budget-buddy\n\n" +
        "A personal budget tracker web app built with React and TypeScript. The backend is an ASP.NET Core Web API in C# " +
        "that stores expenses in PostgreSQL through Entity Framework Core.\n\n" +
        "Unit tests for the API are written with xUnit.\n\n" +
        "Built by Lotte Peeters as a school project at Thomas More.\n";

    private const string WeatherAppReadme =
        "# weather-app\n\n" +
        "A small weather dashboard in Python. It calls the OpenWeather REST API, caches results in SQLite and shows a " +
        "seven-day forecast.\n\n" +
        "Data is cleaned with pandas before it is plotted.\n";

    private const string KotlinQuizReadme =
        "# kotlin-quiz\n\n" +
        "An Android quiz app written in Kotlin with Jetpack Compose. Questions are loaded from a local JSON file.\n\n" +
        "Work in progress: no UI tests yet.\n";

    private const string CvPage =
        "Lotte Peeters\n" +
        "Mechelen, Belgium\n\n" +
        "Education\n" +
        "Bachelor Applied Computer Science, Thomas More Mechelen, 2024 - 2027\n\n" +
        "Skills\n" +
        "C#, ASP.NET Core, React, TypeScript, Python, SQL, Git\n\n" +
        "Languages\n" +
        "Dutch: native (C2)\n" +
        "English: fluent (C1)\n" +
        "French: good working knowledge (B1)\n";

    private static string RepoMetadata(string repo) => $$"""{"repo":"{{repo}}","url":"https://github.com/lotte-demo/{{repo}}"}""";

    // Repo documents use the "<repo>/README.md" path, so gap actions can name budget-buddy, weather-app and kotlin-quiz.
    private static readonly (SourceKind Kind, string Label, (string Path, string Text, string? Metadata)[] Documents)[] DemoSources =
    [
        (SourceKind.GitHub, "github.com/lotte-demo",
        [
            ("budget-buddy/README.md", BudgetBuddyReadme, RepoMetadata("budget-buddy")),
            ("weather-app/README.md", WeatherAppReadme, RepoMetadata("weather-app")),
            ("kotlin-quiz/README.md", KotlinQuizReadme, RepoMetadata("kotlin-quiz"))
        ]),
        (SourceKind.Cv, "lotte-peeters-cv.pdf", [("cv.pdf#page1", CvPage, null)])
    ];

    // Every quote is copied verbatim from the document at Path (a test checks this with QuoteCheck).
    private static readonly (ProfileItemKind Kind, string Name, string Path, string Quote)[] DemoItems =
    [
        (ProfileItemKind.Project, "budget-buddy", "budget-buddy/README.md", "A personal budget tracker web app built with React and TypeScript."),
        (ProfileItemKind.Project, "weather-app", "weather-app/README.md", "A small weather dashboard in Python."),
        (ProfileItemKind.Project, "kotlin-quiz", "kotlin-quiz/README.md", "An Android quiz app written in Kotlin with Jetpack Compose."),
        (ProfileItemKind.Skill, "C# and ASP.NET Core", "budget-buddy/README.md", "The backend is an ASP.NET Core Web API in C#"),
        (ProfileItemKind.Skill, "React with TypeScript", "budget-buddy/README.md", "built with React and TypeScript"),
        (ProfileItemKind.Skill, "Python", "weather-app/README.md", "A small weather dashboard in Python."),
        (ProfileItemKind.Skill, "SQL", "cv.pdf#page1", "C#, ASP.NET Core, React, TypeScript, Python, SQL, Git"),
        (ProfileItemKind.Skill, "Kotlin", "kotlin-quiz/README.md", "written in Kotlin with Jetpack Compose"),
        (ProfileItemKind.Education, "Bachelor Applied Computer Science, Thomas More", "cv.pdf#page1", "Bachelor Applied Computer Science, Thomas More Mechelen, 2024 - 2027"),
        (ProfileItemKind.Language, "Dutch", "cv.pdf#page1", "Dutch: native (C2)"),
        (ProfileItemKind.Language, "English", "cv.pdf#page1", "English: fluent (C1)"),
        (ProfileItemKind.Language, "French", "cv.pdf#page1", "French: good working knowledge (B1)")
    ];

    private sealed record Demo(
        string Company, string Title, string City, PostingLanguage Language, DateOnly Deadline, string Text,
        (string, Importance)[] Requirements, string GenericAnswer);

    // The generic answers below were written by a general-purpose AI assistant given only the posting text and the
    // question "What do I need to get this internship?" (no CV, no profile). They are labelled as a generic chatbot
    // answer with this capture date in the UI. Swap in a real ChatGPT capture before the demo if the team wants one.
    private static readonly DateTimeOffset GenericAnswerCapturedAt = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly Demo[] Demos =
    [
        new("Brightlane", "Full-stack .NET stagiair", "Mechelen", PostingLanguage.Nl, new(2026, 12, 12),
            "Demo data. Brightlane bouwt planningssoftware voor zorginstellingen. Als stagiair werk je mee aan onze webapp. " +
            "Wat verwachten we: C# en ASP.NET Core, React met TypeScript, Docker, geautomatiseerde tests. Een CI-pipeline is een pluspunt.",
            [("C# and ASP.NET Core", Importance.Required), ("React with TypeScript", Importance.Required), ("Docker", Importance.Required),
             ("Automated tests", Importance.Required), ("CI pipeline", Importance.Nice)],
            "Great opportunity! To land this internship at Brightlane, focus on these areas:\n\n" +
            "1. C# and ASP.NET Core: make sure you understand the basics of building web APIs, dependency injection and Entity Framework.\n" +
            "2. React with TypeScript: practise building components, managing state and calling APIs.\n" +
            "3. Docker: learn how to write a Dockerfile and run containers locally.\n" +
            "4. Automated tests: get familiar with unit testing frameworks such as xUnit or NUnit.\n" +
            "5. CI pipelines (a plus): try GitHub Actions or Azure DevOps.\n\n" +
            "Tips: build a small full-stack project that combines these technologies and put it on GitHub. Tailor your CV " +
            "to highlight relevant coursework and projects, and write a motivation letter (in Dutch) explaining why you are " +
            "interested in healthcare software. Good luck!"),
        new("Northwind Data", "Data engineering intern", "Brussels", PostingLanguage.En, new(2026, 12, 20),
            "Demo data. Northwind Data helps retailers make sense of their sales data. As an intern you build and monitor data pipelines. " +
            "You know Python and SQL and you write English at C1 level. Experience with Azure is a plus.",
            [("Python", Importance.Required), ("SQL", Importance.Required), ("Cloud basics (Azure)", Importance.Nice), ("English C1", Importance.Required)],
            "To get this data engineering internship, you should work on:\n\n" +
            "- Python: data manipulation with pandas, writing clean scripts and basic automation.\n" +
            "- SQL: joins, aggregations, window functions and query optimisation.\n" +
            "- English at C1 level: practise technical writing and presenting your work clearly.\n" +
            "- Azure (a plus): explore Azure Data Factory or take the free AZ-900 fundamentals course.\n" +
            "- Data pipelines: learn the basics of ETL and tools like Airflow.\n\n" +
            "Consider building a portfolio project, for example a pipeline that loads a public sales dataset into a database " +
            "and produces a dashboard. Highlight any data-related coursework on your CV and show enthusiasm for retail analytics " +
            "in your application. Good luck!"),
        new("Atelier Lumen", "Stagiaire développeur mobile", "Liège", PostingLanguage.Fr, new(2027, 1, 10),
            "Demo data. Atelier Lumen crée des applications mobiles pour des musées. Vous développez en Kotlin pour Android " +
            "et vous parlez français (B2). Des notions de tests d'interface sont un atout.",
            [("Kotlin / Android", Importance.Required), ("French B2", Importance.Required), ("UI testing", Importance.Nice)],
            "Here is what you need for this mobile developer internship at Atelier Lumen:\n\n" +
            "1. Kotlin and Android: learn Android Studio, Jetpack Compose and the activity lifecycle. Publishing a simple app helps.\n" +
            "2. French at B2 level: you should be able to communicate comfortably with the team. Practise speaking and consider a certificate.\n" +
            "3. UI testing (a plus): look into Espresso or Compose testing.\n\n" +
            "Since they build apps for museums, a portfolio app with a cultural or interactive theme would stand out. Write " +
            "your application in French, keep your CV to one page and mention any mobile projects. Good luck!"),
        new("Havenstad Logistics", "Software stagiair", "Antwerpen", PostingLanguage.Nl, new(2027, 1, 24),
            "Demo data. Havenstad Logistics volgt containers door de haven van Antwerpen. Je kent Java of C#, je kunt REST API's bouwen " +
            "en een goede Git-workflow is mooi meegenomen.",
            [("Java or C#", Importance.Required), ("REST APIs", Importance.Required), ("Git workflow", Importance.Nice)],
            "To get this software internship at Havenstad Logistics, make sure you have:\n\n" +
            "- Java or C#: solid object-oriented programming fundamentals.\n" +
            "- REST APIs: know HTTP methods, status codes and how to build an API with Spring Boot or ASP.NET Core.\n" +
            "- Git workflow (a plus): branching, pull requests and resolving merge conflicts.\n\n" +
            "Build a small API project, for example a tracking service, and host the code on GitHub with a clear README. " +
            "Show interest in logistics and the port of Antwerp in your motivation letter, and prepare to explain your " +
            "projects in the interview. Good luck!")
    ];
}
