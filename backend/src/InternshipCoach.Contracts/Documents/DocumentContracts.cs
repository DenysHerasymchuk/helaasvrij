namespace InternshipCoach.Contracts.Documents;

/// <param name="Language">nl, fr or en; null = the posting's language.</param>
/// <param name="PostingId">One of the user's saved postings; when set, its text is used instead of <paramref name="PostingText" />.</param>
public sealed record TailorCvRequest(string? CvText, string? PostingText, string? Language, Guid? PostingId = null);

/// <summary>Text pulled from an uploaded CV file, for the student to check and edit before tailoring.</summary>
public sealed record CvTextResponse(string Text);

public sealed record UpdateDocumentRequest(string? Title, string? Content);

/// <param name="DroppedCount">Only set by the tailor endpoint: lines removed because their quote was not in the CV.</param>
public sealed record DocumentResponse(
    Guid Id, string Kind, string Language, string Title, string Content, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int? DroppedCount);

public sealed record DocumentSummaryResponse(Guid Id, string Kind, string Language, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
