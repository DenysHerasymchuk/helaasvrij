using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

/// <summary>Development only: gives the dev test user the four synthetic demo postings. Safe to run again.</summary>
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
        if (await db.Postings.AnyAsync(p => p.IsDemo, ct))
        {
            return;
        }

        var now = new CreatedAt(clock.UtcNow);
        foreach (var demo in Demos)
        {
            db.Postings.Add(Posting.Create(user.Id, demo.Company, demo.Title, demo.City, demo.Language, null, demo.Text,
                demo.Deadline, demo.Requirements, isDemo: true, now));
        }

        await db.SaveChangesAsync(ct);
    }

    private sealed record Demo(
        string Company, string Title, string City, PostingLanguage Language, DateOnly Deadline, string Text,
        (string, Importance)[] Requirements);

    private static readonly Demo[] Demos =
    [
        new("Brightlane", "Full-stack .NET stagiair", "Mechelen", PostingLanguage.Nl, new(2026, 12, 12),
            "Demo data. Brightlane bouwt planningssoftware voor zorginstellingen. Als stagiair werk je mee aan onze webapp. " +
            "Wat verwachten we: C# en ASP.NET Core, React met TypeScript, Docker, geautomatiseerde tests. Een CI-pipeline is een pluspunt.",
            [("C# and ASP.NET Core", Importance.Required), ("React with TypeScript", Importance.Required), ("Docker", Importance.Required),
             ("Automated tests", Importance.Required), ("CI pipeline", Importance.Nice)]),
        new("Northwind Data", "Data engineering intern", "Brussels", PostingLanguage.En, new(2026, 12, 20),
            "Demo data. Northwind Data helps retailers make sense of their sales data. As an intern you build and monitor data pipelines. " +
            "You know Python and SQL and you write English at C1 level. Experience with Azure is a plus.",
            [("Python", Importance.Required), ("SQL", Importance.Required), ("Cloud basics (Azure)", Importance.Nice), ("English C1", Importance.Required)]),
        new("Atelier Lumen", "Stagiaire développeur mobile", "Liège", PostingLanguage.Fr, new(2027, 1, 10),
            "Demo data. Atelier Lumen crée des applications mobiles pour des musées. Vous développez en Kotlin pour Android " +
            "et vous parlez français (B2). Des notions de tests d'interface sont un atout.",
            [("Kotlin / Android", Importance.Required), ("French B2", Importance.Required), ("UI testing", Importance.Nice)]),
        new("Havenstad Logistics", "Software stagiair", "Antwerpen", PostingLanguage.Nl, new(2027, 1, 24),
            "Demo data. Havenstad Logistics volgt containers door de haven van Antwerpen. Je kent Java of C#, je kunt REST API's bouwen " +
            "en een goede Git-workflow is mooi meegenomen.",
            [("Java or C#", Importance.Required), ("REST APIs", Importance.Required), ("Git workflow", Importance.Nice)])
    ];
}
