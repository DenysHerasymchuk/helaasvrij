using InternshipCoach.Domain.Sprints;

namespace InternshipCoach.Application.Sprints;

// Reads see only the signed-in user's sprints (global query filter).
public interface ISprintRepository
{
    /// <summary>Tracked active sprint, or null when none is running.</summary>
    Task<Sprint?> FindActiveAsync(CancellationToken cancellationToken);

    /// <summary>Tracked sprint, or null when it does not exist or belongs to someone else.</summary>
    Task<Sprint?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<Sprint>> ListAsync(CancellationToken cancellationToken);

    Task AddAsync(Sprint sprint, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}
