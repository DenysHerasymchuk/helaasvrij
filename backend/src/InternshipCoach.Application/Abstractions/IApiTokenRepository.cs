using InternshipCoach.Domain.Tokens;

namespace InternshipCoach.Application.Abstractions;

public interface IApiTokenRepository
{
    // The signed-in user's tokens only (query filter), newest first.
    Task<IReadOnlyList<ApiToken>> ListAsync(CancellationToken cancellationToken);

    Task<ApiToken?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task AddAsync(ApiToken token, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Called while signing a request in, before any user is known, so it bypasses the user filter.
    /// Returns the owner of an active token and notes the use, or null.
    /// </summary>
    Task<Guid?> ResolveAsync(string hash, DateTimeOffset now, CancellationToken cancellationToken);
}
