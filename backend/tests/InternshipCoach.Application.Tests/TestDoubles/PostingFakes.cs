using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeCurrentUser(Guid userId) : ICurrentUser
{
    public Guid UserId { get; } = userId;
}

public sealed class FakePostingRepository : IPostingRepository
{
    public List<Posting> Items { get; } = [];

    public int Saves { get; private set; }

    public Task AddAsync(Posting posting, CancellationToken ct)
    {
        Items.Add(posting);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Posting>> ListAsync(PostingLanguage? language, string? query, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Posting>>(Items
            .Where(p => language is null || p.Language == language)
            .Where(p => query is null || p.Company.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList());

    public Task<Posting?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(p => p.Id == id));

    public Task SaveAsync(CancellationToken ct)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Posting posting, CancellationToken ct)
    {
        Items.Remove(posting);
        return Task.CompletedTask;
    }
}

public sealed class FakePageFetcher(string? html = null, string? failure = null) : IPageFetcher
{
    public List<Uri> Requested { get; } = [];

    public Task<string> FetchHtmlAsync(Uri url, CancellationToken ct)
    {
        Requested.Add(url);
        return failure is null ? Task.FromResult(html ?? string.Empty) : throw new PageFetchException(failure);
    }
}
