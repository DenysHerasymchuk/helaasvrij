namespace InternshipCoach.Contracts.Settings;

public sealed record SettingsResponse(
    string DefaultLanguage, int StrongMatchThreshold, bool EmailAlerts, IReadOnlyList<string> DocumentSkills, IReadOnlyList<string> InterviewSkills);

/// <param name="DocumentSkills">Writing skill ids for CVs, letters and messages. Null leaves them as they are.</param>
/// <param name="InterviewSkills">Writing skill ids for interview questions and feedback. Null leaves them as they are.</param>
public sealed record UpdateSettingsRequest(
    string? DefaultLanguage, int StrongMatchThreshold, bool EmailAlerts, IReadOnlyList<string>? DocumentSkills = null, IReadOnlyList<string>? InterviewSkills = null);

public sealed record WritingSkillResponse(string Id, string Name, string Description);

public sealed record DeleteAccountRequest(string? Confirm);
