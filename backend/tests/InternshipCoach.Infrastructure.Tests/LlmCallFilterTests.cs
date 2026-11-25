using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Llm;
using InternshipCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Tests;

public class LlmCallFilterTests
{
    private static readonly Guid UserA = Guid.NewGuid();
    private static readonly Guid UserB = Guid.NewGuid();

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private static async Task Add(DbContextOptions<InternshipCoachDbContext> options, Guid user)
    {
        await using var db = new InternshipCoachDbContext(options, new FakeCurrentUser(user));
        db.LlmCalls.Add(LlmCall.Create(user, "f", "m", 1, 1, 1, true, new CreatedAt(DateTimeOffset.UtcNow)));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Query_filter_hides_another_users_llm_calls_and_cap_count_is_per_user()
    {
        var options = new DbContextOptionsBuilder<InternshipCoachDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await Add(options, UserA);
        await Add(options, UserB);
        await Add(options, UserB);

        await using var asA = new InternshipCoachDbContext(options, new FakeCurrentUser(UserA));
        Assert.Equal(UserA, Assert.Single(await asA.LlmCalls.ToListAsync()).UserId);

        var count = await new LlmCallLog(asA).CountSinceAsync(UserB, DateTimeOffset.UtcNow.AddDays(-1), CancellationToken.None);
        Assert.Equal(0, count);
    }
}
