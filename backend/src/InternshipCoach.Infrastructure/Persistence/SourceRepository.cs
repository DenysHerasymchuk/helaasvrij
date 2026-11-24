using InternshipCoach.Application.Sources;
using InternshipCoach.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

// The global user filter on Source scopes every query to the signed-in user.
public sealed class SourceRepository(InternshipCoachDbContext db) : ISourceRepository
{
    public async Task<IReadOnlyList<SourceSummary>> ListAsync(CancellationToken ct) =>
        await db.Sources
            .OrderBy(source => source.Kind)
            .ThenBy(source => source.Label)
            .Select(source => new SourceSummary(
                source.Id, source.Kind, source.Label, source.Status, source.LastSyncedAt, source.Error, source.Documents.Count()))
            .ToListAsync(ct);

    public Task<Source?> FindByKindAsync(SourceKind kind, CancellationToken ct) =>
        db.Sources.Include(source => source.Documents).FirstOrDefaultAsync(source => source.Kind == kind, ct);

    public Task<Source?> FindAsync(Guid id, CancellationToken ct) =>
        db.Sources.Include(source => source.Documents).FirstOrDefaultAsync(source => source.Id == id, ct);

    public async Task AddAsync(Source source, CancellationToken ct)
    {
        db.Sources.Add(source);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task RemoveAsync(Source source, CancellationToken ct)
    {
        db.Sources.Remove(source);
        await db.SaveChangesAsync(ct);
    }
}
