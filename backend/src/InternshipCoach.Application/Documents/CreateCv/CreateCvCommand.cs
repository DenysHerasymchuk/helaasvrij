namespace InternshipCoach.Application.Documents.CreateCv;

/// <param name="Language">nl, fr or en; null = the user's default language setting, else en.</param>
public sealed record CreateCvCommand(string? Language);
