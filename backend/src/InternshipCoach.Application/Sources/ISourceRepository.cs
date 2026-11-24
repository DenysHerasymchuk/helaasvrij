using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Sources;

// Every method is scoped to the signed-in user by the data layer.
public interface ISourceRepository
{
    Task<IReadOnlyList<SourceSummary>> ListAsync(CancellationToken ct);

    Task<Source?> FindByKindAsync(SourceKind kind, CancellationToken ct);

    Task<Source?> FindAsync(Guid id, CancellationToken ct);

    // Persist immediately.
    Task AddAsync(Source source, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);

    Task RemoveAsync(Source source, CancellationToken ct);
}
