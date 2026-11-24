using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Llm;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Users;
using InternshipCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Tests;

public class DemoDataSeederTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    private sealed class FixedUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private static async Task<Guid> AddUser(DbContextOptions<InternshipCoachDbContext> options, string email)
    {
        await using var db = new InternshipCoachDbContext(options);
        Assert.True(Email.TryCreate(email, out var address));
        var user = User.CreateWithPassword(address, email, "hash", new CreatedAt(Now));
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Fact]
    public async Task Seeds_only_the_demo_user_and_is_safe_to_run_twice()
    {
        var options = new DbContextOptionsBuilder<InternshipCoachDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var demo = await AddUser(options, "lotte@demo.test");
        var other = await AddUser(options, "someone@else.test");

        await DemoDataSeeder.SeedPostingsAsync(options, new FixedClock(), "lotte@demo.test", default);
        await DemoDataSeeder.SeedPostingsAsync(options, new FixedClock(), "lotte@demo.test", default);

        // Read without a user filter: nothing at all may belong to the other user.
        await using (var all = new InternshipCoachDbContext(options))
        {
            Assert.All(all.Postings.IgnoreQueryFilters(), p => Assert.Equal(demo, p.UserId));
            Assert.All(all.Sources.IgnoreQueryFilters(), s => Assert.Equal(demo, s.UserId));
            Assert.All(all.ProfileItems.IgnoreQueryFilters(), i => Assert.Equal(demo, i.UserId));
            Assert.All(all.ProfileBasics.IgnoreQueryFilters(), b => Assert.Equal(demo, b.UserId));
        }

        await using var db = new InternshipCoachDbContext(options, new FixedUser(demo));
        var postings = await db.Postings.ToListAsync();
        Assert.Equal(4, postings.Count);
        Assert.All(postings, p => Assert.True(p.IsDemo));

        var basics = await db.ProfileBasics.SingleAsync();
        Assert.Equal(new DateOnly(2027, 2, 1), basics.StageStart);
        var fits = postings.Select(p => p.CheckStage(basics.StageStart, basics.StageEnd)).ToList();
        Assert.Equal(3, fits.Count(f => f == StageFit.Fits));
        Assert.Equal(1, fits.Count(f => f == StageFit.Conflict));

        var items = await db.ProfileItems.Include(i => i.Evidence).ToListAsync();
        Assert.Equal(12, items.Count);
        Assert.All(items, i => Assert.True(i.Confirmed));
        Assert.Equal(["C2", "C1", "B1"], items.Where(i => i.Kind == ProfileItemKind.Language).OrderBy(i => i.Name).Select(i => i.Level));

        var texts = await db.SourceDocuments.ToDictionaryAsync(d => d.Id, d => d.Text);
        Assert.Equal(4, texts.Count);
        Assert.All(items.SelectMany(i => i.Evidence), e => Assert.True(QuoteCheck.IsVerbatim(e.Quote, texts[e.SourceDocumentId]), e.Quote));

        var repos = await db.SourceDocuments.Where(d => d.Path.EndsWith("/README.md")).Select(d => d.Path).OrderBy(p => p).ToListAsync();
        Assert.Equal(["budget-buddy/README.md", "kotlin-quiz/README.md", "weather-app/README.md"], repos);

        await using var asOther = new InternshipCoachDbContext(options, new FixedUser(other));
        Assert.Empty(await asOther.Postings.ToListAsync());
        Assert.Empty(await asOther.ProfileItems.ToListAsync());
    }

    [Fact]
    public async Task Backfills_missing_profile_on_an_existing_demo_database()
    {
        var options = new DbContextOptionsBuilder<InternshipCoachDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var demo = await AddUser(options, "lotte@demo.test");
        await using (var old = new InternshipCoachDbContext(options, new FixedUser(demo)))
        {
            // What the old seeder left behind: a demo posting without stage dates, and a school but no stage period.
            old.Postings.Add(Posting.Create(demo, "Brightlane", "Old", "Mechelen", PostingLanguage.Nl, null, "text", null,
                [("C#", Importance.Required)], isDemo: true, new CreatedAt(Now)));
            old.ProfileBasics.Add(ProfileBasics.Create(demo, "My school", null, null));
            await old.SaveChangesAsync();
        }

        await DemoDataSeeder.SeedPostingsAsync(options, new FixedClock(), "lotte@demo.test", default);

        await using var db = new InternshipCoachDbContext(options, new FixedUser(demo));
        Assert.Equal(4, await db.Postings.CountAsync());
        var brightlane = await db.Postings.SingleAsync(p => p.Company == "Brightlane");
        Assert.Equal("Old", brightlane.Title);
        Assert.NotNull(brightlane.StageStart);
        Assert.NotNull(brightlane.GenericAnswer);
        var basics = await db.ProfileBasics.SingleAsync();
        Assert.Equal("My school", basics.School);
        Assert.Equal(new DateOnly(2027, 5, 31), basics.StageEnd);
        Assert.Equal(12, await db.ProfileItems.CountAsync());
    }
}
