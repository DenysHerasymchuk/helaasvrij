using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Documents;
using InternshipCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Tests;

public class DocumentFilterTests
{
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private static async Task<Document> Add(DbContextOptions<InternshipCoachDbContext> options, Guid user, DateTimeOffset at)
    {
        await using var db = new InternshipCoachDbContext(options, new FakeCurrentUser(user));
        var document = Document.Create(user, DocumentKind.Cv, "en", "CV", "# CV", new CreatedAt(at));
        await new DocumentRepository(db).AddAsync(document, CancellationToken.None);
        return document;
    }

    [Fact]
    public async Task Another_users_document_is_invisible_and_list_is_newest_first()
    {
        var options = new DbContextOptionsBuilder<InternshipCoachDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var older = await Add(options, UserA, DateTimeOffset.UtcNow.AddDays(-1));
        var newer = await Add(options, UserA, DateTimeOffset.UtcNow);
        var other = await Add(options, UserB, DateTimeOffset.UtcNow);

        await using var asA = new InternshipCoachDbContext(options, new FakeCurrentUser(UserA));
        var repo = new DocumentRepository(asA);

        Assert.Equal([newer.Id, older.Id], (await repo.ListAsync(CancellationToken.None)).Select(d => d.Id));
        Assert.Null(await repo.FindAsync(other.Id, CancellationToken.None));
    }
}
