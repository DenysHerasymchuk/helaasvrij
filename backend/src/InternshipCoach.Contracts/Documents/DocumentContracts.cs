namespace InternshipCoach.Contracts.Documents;

/// <param name="Language">nl, fr or en; null = the posting's language.</param>
public sealed record TailorCvRequest(string? CvText, string? PostingText, string? Language);

/// <param name="Kind">coverLetter or recruiterMessage.</param>
/// <param name="Language">nl, fr or en; null = the posting's language.</param>
public sealed record DraftDocumentRequest(Guid PostingId, string? Kind, string? Language);

public sealed record UpdateDocumentRequest(string? Title, string? Content);

/// <param name="DroppedCount">Only set by the tailor endpoint: lines removed because their quote was not in the CV.</param>
public sealed record DocumentResponse(
    Guid Id, string Kind, string Language, string Title, string Content, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int? DroppedCount);

public sealed record DocumentSummaryResponse(Guid Id, string Kind, string Language, string Title, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
