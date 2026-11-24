using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Sources;
using InternshipCoach.Application.Sources.GitHub;
using InternshipCoach.Domain.Sources;
using InternshipCoach.Domain.Users;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeCurrentUser(Guid userId) : ICurrentUser
{
    public Guid UserId { get; } = userId;
}

public sealed class FakeSourceRepository : ISourceRepository
{
    public List<Source> Items { get; } = [];

    public Task<IReadOnlyList<SourceSummary>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<SourceSummary>>(Items.Select(SourceSummary.From).ToList());

    public Task<Source?> FindByKindAsync(SourceKind kind, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Kind == kind));

    public Task<Source?> FindAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

    public Task AddAsync(Source source, CancellationToken ct)
    {
        Items.Add(source);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;

    public Task RemoveAsync(Source source, CancellationToken ct)
    {
        Items.Remove(source);
        return Task.CompletedTask;
    }
}

public sealed class FakeGitHubClient : IGitHubClient
{
    public IReadOnlyList<GitHubRepoData> Repos { get; set; } = [];
    public string? FailWith { get; set; }
    public string? LastUsername { get; private set; }

    public Task<IReadOnlyList<GitHubRepoData>> FetchReposAsync(string username, int max, CancellationToken ct)
    {
        LastUsername = username;
        return FailWith is null ? Task.FromResult(Repos) : throw new SourceFetchException(FailWith);
    }
}

public sealed class FakePdfTextExtractor(params string[] pages) : IPdfTextExtractor
{
    public IReadOnlyList<string> ExtractPages(byte[] pdf) => pages;
}

public sealed class FakeWebPageFetcher(string? text, string? failWith = null) : IWebPageFetcher
{
    public Task<string> FetchTextAsync(Uri url, CancellationToken ct) =>
        failWith is null ? Task.FromResult(text ?? string.Empty) : throw new SourceFetchException(failWith);
}

public sealed class FakeUserLookup(User? user) : IUserRepository
{
    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(user);
    public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
    public Task<User?> FindByGitHubIdAsync(long gitHubId, CancellationToken cancellationToken) => Task.FromResult<User?>(null);
    public Task AddAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
}
