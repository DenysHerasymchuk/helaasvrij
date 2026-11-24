namespace InternshipCoach.Contracts.Postings;

public sealed record PastePostingRequest(string? Text, string? Url);

public sealed record UrlPostingRequest(string? Url);

public sealed record UpdatePostingDatesRequest(DateOnly? Deadline, DateTimeOffset? InterviewAt);

public sealed record RequirementResponse(Guid Id, string Text, string Importance);

public sealed record PostingSummaryResponse(
    Guid Id, string Company, string Title, string City, string Language, DateOnly? Deadline, DateTimeOffset? InterviewAt,
    bool IsDemo, int RequirementCount, DateTimeOffset CreatedAt);

public sealed record PostingDetailResponse(
    Guid Id, string Company, string Title, string City, string Language, string? Url, string RawText, DateOnly? Deadline,
    DateTimeOffset? InterviewAt, bool IsDemo, DateTimeOffset CreatedAt, IReadOnlyList<RequirementResponse> Requirements);
