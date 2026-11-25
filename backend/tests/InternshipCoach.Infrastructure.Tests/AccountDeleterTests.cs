using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Documents;
using InternshipCoach.Domain.Interview;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Llm;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;
using InternshipCoach.Domain.Sprints;
using InternshipCoach.Domain.Tokens;
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
        var posting = Posting.Create(user.Id, "Acme", "Intern", null, PostingLanguage.Nl, null, "text", null,
            [("Knows Docker", Importance.Required)], false, new CreatedAt(DateTimeOffset.UtcNow));
        posting.RecordSnapshot("analyse", DateTimeOffset.UtcNow);
        db.Postings.Add(posting);
        var sprint = Sprint.Start(user.Id, "Sprint 1", "Apply", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15), new CreatedAt(DateTimeOffset.UtcNow));
        db.Sprints.Add(sprint);
        db.ApiTokens.Add(ApiToken.Create(user.Id, "MCP", new CreatedAt(DateTimeOffset.UtcNow), out _));
        db.Applications.Add(JobApplication.Create(user.Id, posting.Id, new CreatedAt(DateTimeOffset.UtcNow), sprint.Id));
        var source = Source.Create(user.Id, SourceKind.GitHub, "gh");
        var document = source.AddDocument("a/summary", "Repository: a");
        db.Sources.Add(source);
        db.ProfileItems.Add(ProfileItem.Create(user.Id, ProfileItemKind.Project, "a", document.Id, "Repository: a", new CreatedAt(DateTimeOffset.UtcNow)));
        db.Sources.Add(Source.Create(user.Id, SourceKind.GitHub, "gh"));
        db.Documents.Add(Document.Create(user.Id, DocumentKind.Cv, "en", "CV", "# CV", new CreatedAt(DateTimeOffset.UtcNow)));
        var question = InterviewQuestion.Create(user.Id, posting.Id, QuestionSource.Gap, "How would you add Docker to a?", "a", null, 0, new CreatedAt(DateTimeOffset.UtcNow));
        db.InterviewQuestions.Add(question);
        db.InterviewAnswers.Add(InterviewAnswer.Create(user.Id, posting.Id, question.Id, "I would add a Dockerfile.", "Clear", "Add compose", "I added a Dockerfile to a.", new CreatedAt(DateTimeOffset.UtcNow)));
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
        await using (var shared = new InternshipCoachDbContext(options, new FakeCurrentUser(victim.Id)))
        {
            shared.CompanyQuestions.Add(CompanyQuestion.Create("Acme", "Why Acme?"));
            await shared.SaveChangesAsync();
        }


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

        // Shared company questions are not user data and must survive.
        Assert.NotEmpty(await check.CompanyQuestions.ToListAsync());

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
