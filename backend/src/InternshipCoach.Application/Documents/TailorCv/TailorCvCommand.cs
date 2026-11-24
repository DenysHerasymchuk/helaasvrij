namespace InternshipCoach.Application.Documents.TailorCv;

/// <param name="Language">nl, fr or en; null = the posting's language (the model decides).</param>
/// <param name="PostingId">One of the signed-in user's saved postings; when set, its text replaces <paramref name="PostingText" />.</param>
public sealed record TailorCvCommand(string? CvText, string? PostingText, string? Language, Guid? PostingId = null);
