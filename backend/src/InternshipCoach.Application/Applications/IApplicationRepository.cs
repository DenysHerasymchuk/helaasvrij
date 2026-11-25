using InternshipCoach.Domain.Applications;

namespace InternshipCoach.Application.Applications;

/// <summary>An application with the few posting fields a board card shows.</summary>
public sealed record ApplicationCard(JobApplication Application, string Company, string Title, string City, DateOnly? Deadline, string Key);

// Reads see only the signed-in user's applications (global query filter).
public interface IApplicationRepository
{
    /// <summary>Newest stage change first.</summary>
    Task<IReadOnlyList<ApplicationCard>> ListAsync(CancellationToken cancellationToken);

    Task<ApplicationCard?> GetCardAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tracked entity, or null when it does not exist or belongs to someone else.</summary>
    Task<JobApplication?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Id of the application for this posting, or null when it is not on the board.</summary>
    Task<Guid?> FindIdForPostingAsync(Guid postingId, CancellationToken cancellationToken);

    /// <summary>Tracked applications that are in this sprint.</summary>
    Task<IReadOnlyList<JobApplication>> ListForSprintAsync(Guid sprintId, CancellationToken cancellationToken);

    /// <summary>Every history event of the signed-in user's applications, oldest first.</summary>
    Task<IReadOnlyList<ApplicationEvent>> ListEventsAsync(CancellationToken cancellationToken);

    /// <summary>History of one application, oldest first. Empty when it does not exist or belongs to someone else.</summary>
    Task<IReadOnlyList<ApplicationEvent>> ListEventsAsync(Guid applicationId, CancellationToken cancellationToken);

    Task AddAsync(JobApplication application, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}
