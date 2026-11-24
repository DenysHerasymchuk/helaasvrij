using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Applications;
using InternshipCoach.Application.Dashboard;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Sources;
using InternshipCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Tests;

public class DashboardOwnershipTests
{
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    private sealed class FixedUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private static async Task Seed(DbContextOptions<InternshipCoachDbContext> options, Guid user, string company)
    {
        await using var db = new InternshipCoachDbContext(options, new FixedUser(user));
        var posting = Posting.Create(user, company, "Intern", "Gent", PostingLanguage.En, null, "text", null, [("C#", Importance.Required)], false, new CreatedAt(Now));
        db.Postings.Add(posting);
        var application = JobApplication.Create(user, posting.Id, new CreatedAt(Now));
        application.MoveTo(ApplicationStage.Applied, Now);
        db.Applications.Add(application);
        db.Sources.Add(Source.Create(user, SourceKind.GitHub, company + "-gh"));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Dashboard_only_shows_the_signed_in_users_data()
    {
        var options = new DbContextOptionsBuilder<InternshipCoachDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await Seed(options, UserA, "Mine");
        await Seed(options, UserB, "Theirs");

        await using var db = new InternshipCoachDbContext(options, new FixedUser(UserA));
        var handler = new GetDashboardHandler(
            new SourceRepository(db), new PostingRepository(db),
            new GetApplicationStatsHandler(new ApplicationRepository(db), new FixedClock()));

        var result = await handler.HandleAsync(CancellationToken.None);

        Assert.Equal(["Mine"], result.Postings.Select(p => p.Company));
        Assert.Equal("Mine-gh", result.Sources.Single(s => s.Kind == "GitHub").Label);
        Assert.Equal(1, result.ApplicationsByMonth.Sum(m => m.Applied));
    }
}
