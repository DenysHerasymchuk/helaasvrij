namespace InternshipCoach.Contracts.Applications;

public sealed record CreateApplicationRequest(Guid PostingId);

/// <param name="Stage">saved, applied, interview, offer or rejected; null leaves it unchanged.</param>
/// <param name="Notes">Null leaves the notes unchanged.</param>
public sealed record UpdateApplicationRequest(string? Stage, string? Notes);

/// <param name="SprintId">Null while the position sits in the backlog.</param>
/// <param name="Key">Short position key such as BRI-12.</param>
public sealed record ApplicationResponse(
    Guid Id, Guid PostingId, string Company, string Title, string City, DateOnly? Deadline, string Stage,
    DateTimeOffset? AppliedAt, DateTimeOffset StageChangedAt, string Notes, DateTimeOffset CreatedAt,
    Guid? SprintId = null, string Key = "");

public sealed record ApplicationMonthResponse(string Month, int Applied, int Interview, int Offer);

/// <param name="Type">added, stageChanged or sprintChanged.</param>
/// <param name="SprintName">The sprint the card was in (the new sprint for sprintChanged); null in the backlog.</param>
/// <summary>One line of the home page feed: what happened to which position.</summary>
public sealed record ActivityItemResponse(
    string Type, string? FromStage, string? ToStage, Guid PostingId, string Key, string Title, string Company, string? SprintName, DateTimeOffset At);

public sealed record ApplicationEventResponse(string Type, string? FromStage, string? ToStage, Guid? SprintId, string? SprintName, DateTimeOffset At);
