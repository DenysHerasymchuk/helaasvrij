using InternshipCoach.Domain.Documents;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Llm;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Sources;
using InternshipCoach.Domain.Settings;
using InternshipCoach.Domain.Users;
using InternshipCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Tests;

public class AccountDeleterTests
{
    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private static async Task<User> Seed(DbContextOptions<InternshipCoachDbContext> options, string email)
    {
        var user = User.CreateWithPassword(Email.TryCreate(email, out var parsed) ? parsed : throw new InvalidOperationException(), "Test", "hash", new CreatedAt(DateTimeOffset.UtcNow));
        await using var db = new InternshipCoachDbContext(options, new FakeCurrentUser(user.Id));
        db.Users.Add(user);
        db.LlmCalls.Add(LlmCall.Create(user.Id, "f", "m", 1, 1, 1, true, new CreatedAt(DateTimeOffset.UtcNow)));
        db.LlmCalls.Add(LlmCall.Create(user.Id, "f", "m", 1, 1, 1, true, new CreatedAt(DateTimeOffset.UtcNow)));
        db.UserSettings.Add(UserSettings.Create(user.Id, "nl", 60, true));
        db.Postings.Add(Posting.Create(user.Id, "Acme", "Intern", null, PostingLanguage.Nl, null, "text", null,
            [("Knows Docker", Importance.Required)], false, new CreatedAt(DateTimeOffset.UtcNow)));
        db.Sources.Add(Source.Create(user.Id, SourceKind.GitHub, "gh"));
        db.Documents.Add(Document.Create(user.Id, DocumentKind.Cv, "en", "CV", "# CV", new CreatedAt(DateTimeOffset.UtcNow)));
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task Deletes_every_row_of_every_user_owned_type_and_the_user_but_nothing_else()
    {
        var options = new DbContextOptionsBuilder<InternshipCoachDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var victim = await Seed(options, "victim@example.com");
        var other = await Seed(options, "other@example.com");

        await using (var db = new InternshipCoachDbContext(options, new FakeCurrentUser(victim.Id)))
        {
            await new AccountDeleter(db).DeleteAsync(victim.Id, CancellationToken.None);
        }

        await using var check = new InternshipCoachDbContext(options, new FakeCurrentUser(Guid.NewGuid()));

        // Every user-owned type in the model: no row left for the victim, the other user's rows intact.
        var ownedTypes = check.Model.GetEntityTypes().Where(type => typeof(IUserOwned).IsAssignableFrom(type.ClrType)).ToList();
        Assert.NotEmpty(ownedTypes);
        foreach (var type in ownedTypes)
        {
            var rows = await Count(check, type.ClrType);
            Assert.DoesNotContain(victim.Id, rows);
            Assert.True(rows.Contains(other.Id), $"{type.ClrType.Name} lost the other user's rows");
        }

        Assert.Null(await check.Users.FirstOrDefaultAsync(user => user.Id == victim.Id));
        Assert.NotNull(await check.Users.FirstOrDefaultAsync(user => user.Id == other.Id));
    }

    private static async Task<List<Guid>> Count(InternshipCoachDbContext db, Type clrType)
    {
        var method = typeof(AccountDeleterTests).GetMethod(nameof(OwnerIds), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return await (Task<List<Guid>>)method.MakeGenericMethod(clrType).Invoke(null, [db])!;
    }

    private static Task<List<Guid>> OwnerIds<T>(InternshipCoachDbContext db) where T : class, IUserOwned =>
        db.Set<T>().IgnoreQueryFilters().Select(row => row.UserId).ToListAsync();
}
