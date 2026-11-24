using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Abstractions;

public interface IPostingRepository
{
    /// <summary>Persists immediately.</summary>
    Task AddAsync(Posting posting, CancellationToken cancellationToken);

    /// <summary>The signed-in user's postings, newest first. <paramref name="query" /> matches company, title or city.</summary>
    Task<IReadOnlyList<Posting>> ListAsync(PostingLanguage? language, string? query, CancellationToken cancellationToken);

    /// <summary>Tracked posting with its requirements, or null when it does not exist or belongs to someone else.</summary>
    Task<Posting?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);

    Task DeleteAsync(Posting posting, CancellationToken cancellationToken);
}
