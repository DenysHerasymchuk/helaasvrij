namespace InternshipCoach.Contracts.Applications;

public sealed record CreateApplicationRequest(Guid PostingId);

/// <param name="Stage">saved, applied, interview, offer or rejected; null leaves it unchanged.</param>
/// <param name="Notes">Null leaves the notes unchanged.</param>
public sealed record UpdateApplicationRequest(string? Stage, string? Notes);

public sealed record ApplicationResponse(
    Guid Id, Guid PostingId, string Company, string Title, string City, DateOnly? Deadline, string Stage,
    DateTimeOffset? AppliedAt, DateTimeOffset StageChangedAt, string Notes, DateTimeOffset CreatedAt);

public sealed record ApplicationMonthResponse(string Month, int Applied, int Interview, int Offer);
