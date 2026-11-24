using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Applications;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Tests;

public class ApplicationFilterTests
{
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private static async Task<JobApplication> Add(DbContextOptions<InternshipCoachDbContext> options, Guid user)
    {
        await using var db = new InternshipCoachDbContext(options, new FakeCurrentUser(user));
        var posting = Posting.Create(user, "Acme", "Intern", "Gent", PostingLanguage.En, null, "text", null, [], false, new CreatedAt(DateTimeOffset.UtcNow));
        db.Postings.Add(posting);
        var application = JobApplication.Create(user, posting.Id, new CreatedAt(DateTimeOffset.UtcNow));
        await new ApplicationRepository(db).AddAsync(application, CancellationToken.None);
        return application;
    }

    [Fact]
    public async Task Another_users_application_is_invisible_and_cards_carry_posting_details()
    {
        var options = new DbContextOptionsBuilder<InternshipCoachDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var mine = await Add(options, UserA);
        var theirs = await Add(options, UserB);

        await using var asA = new InternshipCoachDbContext(options, new FakeCurrentUser(UserA));
        var repo = new ApplicationRepository(asA);

        var cards = await repo.ListAsync(CancellationToken.None);
        Assert.Equal([mine.Id], cards.Select(c => c.Application.Id));
        Assert.Equal("Acme", cards[0].Company);
        Assert.Null(await repo.FindAsync(theirs.Id, CancellationToken.None));
        Assert.Null(await repo.GetCardAsync(theirs.Id, CancellationToken.None));
        Assert.Null(await repo.FindIdForPostingAsync(theirs.PostingId, CancellationToken.None));
    }
}
