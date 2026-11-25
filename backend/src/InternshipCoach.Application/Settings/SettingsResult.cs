using InternshipCoach.Application.Writing;
using InternshipCoach.Domain.Settings;

namespace InternshipCoach.Application.Settings;

public sealed record SettingsResult(
    string DefaultLanguage,
    int StrongMatchThreshold,
    bool EmailAlerts,
    IReadOnlyDictionary<string, string>? Errors = null,
    IReadOnlyList<string>? DocumentSkills = null,
    IReadOnlyList<string>? InterviewSkills = null)
{
    public bool Succeeded => Errors is null;

    public static SettingsResult From(UserSettings settings) =>
        new(settings.DefaultLanguage, settings.StrongMatchThreshold, settings.EmailAlerts, null, settings.DocumentSkillIds, settings.InterviewSkillIds);

    public static SettingsResult Defaults() =>
        new(UserSettings.DefaultLanguageCode, UserSettings.DefaultStrongMatchThreshold, false, null, WritingSkills.DefaultDocuments, WritingSkills.DefaultInterview);

    public static SettingsResult Invalid(IReadOnlyDictionary<string, string> errors) =>
        new(string.Empty, 0, false, errors);
}
