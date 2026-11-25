namespace InternshipCoach.Domain.Applications;

public enum ApplicationEventType
{
    Added,
    StageChanged,
    SprintChanged
}

// One line of a position's history. Written by JobApplication itself, never edited.
public sealed class ApplicationEvent
{
    public Guid Id { get; private set; }
    public Guid ApplicationId { get; private set; }
    public ApplicationEventType Type { get; private set; }
    public ApplicationStage? FromStage { get; private set; }
    public ApplicationStage? ToStage { get; private set; }

    /// <summary>The sprint the application was in when this happened (the new sprint for SprintChanged).</summary>
    public Guid? SprintId { get; private set; }
    public DateTimeOffset At { get; private set; }

    private ApplicationEvent()
    {
    }

    internal static ApplicationEvent Create(
        Guid applicationId, ApplicationEventType type, ApplicationStage? from, ApplicationStage? to, Guid? sprintId, DateTimeOffset at) =>
        new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            Type = type,
            FromStage = from,
            ToStage = to,
            SprintId = sprintId,
            At = at.ToUniversalTime()
        };
}
