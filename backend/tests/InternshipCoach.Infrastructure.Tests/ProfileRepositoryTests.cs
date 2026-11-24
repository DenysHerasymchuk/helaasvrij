using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;
using InternshipCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Tests;

public class ProfileRepositoryTests
{
    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();

    private static async Task<(DbContextOptions<InternshipCoachDbContext> Options, Guid DocumentA)> Seed()
    {
        var options = new DbContextOptionsBuilder<InternshipCoachDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var documentA = Guid.Empty;

        foreach (var user in new[] { UserA, UserB })
        {
            await using var db = new InternshipCoachDbContext(options, new FakeCurrentUser(user));
            var source = Source.Create(user, SourceKind.GitHub, "gh");
            var document = source.AddDocument("a/summary", "Repository: a", """{"repo":"a","url":"https://github.com/x/a"}""");
            source.MarkReady(DateTimeOffset.UtcNow);
            db.Sources.Add(source);
            db.ProfileItems.Add(ProfileItem.Create(user, ProfileItemKind.Project, "a", null, document.Id, "Repository: a", new CreatedAt(DateTimeOffset.UtcNow)));
            await db.SaveChangesAsync();
            if (user == UserA)
            {
                documentA = document.Id;
            }
        }

        return (options, documentA);
    }

    [Fact]
    public async Task A_user_only_sees_and_finds_their_own_items_documents_and_basics()
    {
        var (options, documentA) = await Seed();
        await using var asB = new InternshipCoachDbContext(options, new FakeCurrentUser(UserB));
        var repository = new ProfileRepository(asB);

        var items = await repository.ListItemsAsync(default);
        Assert.All(items, item => Assert.Equal(UserB, item.UserId));
        Assert.Null(await repository.FindItemAsync((await new ProfileRepository(new InternshipCoachDbContext(options, new FakeCurrentUser(UserA))).ListItemsAsync(default)).Single().Id, default));
        Assert.Empty(await repository.GetDocumentRefsAsync([documentA], default));
        Assert.Single(await repository.ListReadyDocumentsAsync(default));
    }

    [Fact]
    public async Task Document_refs_expose_the_repo_url()
    {
        var (options, documentA) = await Seed();
        await using var asA = new InternshipCoachDbContext(options, new FakeCurrentUser(UserA));

        var reference = (await new ProfileRepository(asA).GetDocumentRefsAsync([documentA], default))[documentA];

        Assert.Equal("https://github.com/x/a", reference.Url);
        Assert.Equal("a/summary", reference.Path);
    }
}
