namespace InternshipCoach.Application.Documents.TailorCv;

/// <param name="Language">nl, fr or en; null = the posting's language (the model decides).</param>
public sealed record TailorCvCommand(string? CvText, string? PostingText, string? Language);
