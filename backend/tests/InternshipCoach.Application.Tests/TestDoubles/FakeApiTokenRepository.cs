using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Tokens;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeApiTokenRepository(Guid userId) : IApiTokenRepository
{
    public List<ApiToken> Items { get; } = [];

    // Mirrors the user filter: only the signed-in user's tokens are visible.
    private IEnumerable<ApiToken> Mine => Items.Where(t => t.UserId == userId);

    public Task<IReadOnlyList<ApiToken>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ApiToken>>(Mine.OrderByDescending(t => t.CreatedAt.Value).ToList());

    public Task<ApiToken?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(Mine.FirstOrDefault(t => t.Id == id));

    public Task AddAsync(ApiToken token, CancellationToken ct)
    {
        Items.Add(token);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

    public Task<Guid?> ResolveAsync(string hash, DateTimeOffset now, CancellationToken ct) =>
        Task.FromResult(Items.Where(t => t.Hash == hash && t.IsActive).Select(t => (Guid?)t.UserId).FirstOrDefault());
}
