using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

/// <summary>
/// Development only: gives the dev test user the four synthetic demo postings, each with a stored generic chatbot
/// answer for the side-by-side view. Safe to run again: existing demo postings only get a missing generic answer filled in.
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

        await using var db = new InternshipCoachDbContext(options, new FixedUser(user.Id));
        var existing = await db.Postings.Where(p => p.IsDemo).ToListAsync(ct);
        if (existing.Count > 0)
        {
            foreach (var posting in existing.Where(p => p.GenericAnswer is null))
            {
                var demo = Demos.FirstOrDefault(d => d.Company == posting.Company);
                if (demo is not null)
                {
                    posting.SetGenericAnswer(demo.GenericAnswer, GenericAnswerCapturedAt);
                }
            }

            await db.SaveChangesAsync(ct);
            return;
        }

        var now = new CreatedAt(clock.UtcNow);
        foreach (var demo in Demos)
        {
            var posting = Posting.Create(user.Id, demo.Company, demo.Title, demo.City, demo.Language, null, demo.Text,
                demo.Deadline, demo.Requirements, isDemo: true, now);
            posting.SetGenericAnswer(demo.GenericAnswer, GenericAnswerCapturedAt);
            db.Postings.Add(posting);
        }

        await db.SaveChangesAsync(ct);
    }

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
