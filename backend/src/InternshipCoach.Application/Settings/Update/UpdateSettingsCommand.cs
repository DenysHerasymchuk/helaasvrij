namespace InternshipCoach.Application.Settings.Update;

/// <param name="DocumentSkills">Writing skills for CVs, letters and messages. Null leaves them as they are.</param>
/// <param name="InterviewSkills">Writing skills for interview questions and feedback. Null leaves them as they are.</param>
public sealed record UpdateSettingsCommand(
    string? DefaultLanguage,
    int StrongMatchThreshold,
    bool EmailAlerts,
    IReadOnlyList<string>? DocumentSkills = null,
    IReadOnlyList<string>? InterviewSkills = null);
