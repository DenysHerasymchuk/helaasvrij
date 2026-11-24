namespace InternshipCoach.Contracts.Postings;

public sealed record PastePostingRequest(string? Text, string? Url);

public sealed record UrlPostingRequest(string? Url);

public sealed record UpdatePostingDatesRequest(DateOnly? Deadline, DateTimeOffset? InterviewAt, DateOnly? StageStart, DateOnly? StageEnd);

public sealed record EvidenceSourceResponse(Guid SourceId, string Kind, string Label, string Path, string? Url);

public sealed record RequirementResponse(
    Guid Id, string Text, string Importance, string Coverage, string? EvidenceQuote, EvidenceSourceResponse? EvidenceSource,
    string? Action, string? ActionProject, DateOnly? DueDate, bool Done, DateTimeOffset? AnalysedAt);

/// <summary>
/// The score is computed in code. NoClearFit is only meaningful when Analysed is true. FromDemoCache: demo postings
/// only, the AI call failed or was too slow and the stored analysis was returned instead.
/// </summary>
public sealed record GapAnalysisResponse(bool Analysed, int Score, bool NoClearFit, DateTimeOffset? AnalysedAt, bool FromDemoCache = false);

/// <summary>Generic chatbot answer built from the posting text only. IsDemo: stored text captured for a demo posting.</summary>
public sealed record GenericAnswerResponse(string Text, DateTimeOffset CapturedAt, bool IsDemo);

/// <summary>StageFit: "fits", "conflict" or "unknown" against the student's stage period on the profile.</summary>
public sealed record PostingSummaryResponse(
    Guid Id, string Company, string Title, string City, string Language, DateOnly? Deadline, DateTimeOffset? InterviewAt,
    bool IsDemo, int RequirementCount, DateTimeOffset CreatedAt, DateOnly? StageStart, DateOnly? StageEnd, string StageFit);

public sealed record PostingDetailResponse(
    Guid Id, string Company, string Title, string City, string Language, string? Url, string RawText, DateOnly? Deadline,
    DateTimeOffset? InterviewAt, bool IsDemo, DateTimeOffset CreatedAt, IReadOnlyList<RequirementResponse> Requirements,
    GapAnalysisResponse Analysis, DateOnly? StageStart, DateOnly? StageEnd, string StageFit);
