using InternshipCoach.Application.Sprints;
using InternshipCoach.Domain.Sprints;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

public sealed class SprintRepository(InternshipCoachDbContext db) : ISprintRepository
{
    public Task<Sprint?> FindActiveAsync(CancellationToken cancellationToken) =>
        db.Sprints.FirstOrDefaultAsync(sprint => sprint.Status == SprintStatus.Active, cancellationToken);

    public Task<Sprint?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Sprints.FirstOrDefaultAsync(sprint => sprint.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Sprint>> ListAsync(CancellationToken cancellationToken) =>
        (await db.Sprints.AsNoTracking().ToListAsync(cancellationToken)).OrderByDescending(sprint => sprint.CreatedAt.Value).ToList();

    public async Task AddAsync(Sprint sprint, CancellationToken cancellationToken)
    {
        db.Sprints.Add(sprint);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
