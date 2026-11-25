using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Settings;

// One row per user. Created on the first save; until then the defaults apply.
public sealed class UserSettings : IUserOwned
{
    public const string DefaultLanguageCode = "en";
    public const int DefaultStrongMatchThreshold = 75;
    public const int ThresholdMin = 0;
    public const int ThresholdMax = 100;

    public static readonly IReadOnlyList<string> Languages = ["nl", "fr", "en"];

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string DefaultLanguage { get; private set; } = DefaultLanguageCode;
    public int StrongMatchThreshold { get; private set; } = DefaultStrongMatchThreshold;
    public bool EmailAlerts { get; private set; }

    // Writing skills the AI uses per area, as comma separated ids. The catalog lives in the Application layer.
    public string DocumentSkills { get; private set; } = "humanizer";
    public string InterviewSkills { get; private set; } = "humanizer,teacher";

    public IReadOnlyList<string> DocumentSkillIds => Split(DocumentSkills);
    public IReadOnlyList<string> InterviewSkillIds => Split(InterviewSkills);

    private UserSettings()
    {
    }

    /// <summary>Null leaves an area as it is; an empty list means no skills there.</summary>
    public void SetWritingSkills(IReadOnlyList<string>? documents, IReadOnlyList<string>? interview)
    {
        if (documents is not null)
        {
            DocumentSkills = string.Join(',', documents.Distinct());
        }

        if (interview is not null)
        {
            InterviewSkills = string.Join(',', interview.Distinct());
        }
    }

    private static IReadOnlyList<string> Split(string value) => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public static UserSettings Create(Guid userId, string? defaultLanguage, int strongMatchThreshold, bool emailAlerts)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        var settings = new UserSettings { Id = Guid.NewGuid(), UserId = userId };
        settings.Update(defaultLanguage, strongMatchThreshold, emailAlerts);
        return settings;
    }

    public void Update(string? defaultLanguage, int strongMatchThreshold, bool emailAlerts)
    {
        var errors = Validate(defaultLanguage, strongMatchThreshold);
        if (errors.Count > 0)
        {
            throw new DomainException(string.Join(" ", errors.Values));
        }

        DefaultLanguage = defaultLanguage!.Trim().ToLowerInvariant();
        StrongMatchThreshold = strongMatchThreshold;
        EmailAlerts = emailAlerts;
    }

    /// <summary>Field name (camelCase) to message; empty when valid.</summary>
    public static Dictionary<string, string> Validate(string? defaultLanguage, int strongMatchThreshold)
    {
        var errors = new Dictionary<string, string>();

        if (defaultLanguage is null || !Languages.Contains(defaultLanguage.Trim().ToLowerInvariant()))
        {
            errors["defaultLanguage"] = "Choose Dutch, French or English.";
        }

        if (strongMatchThreshold is < ThresholdMin or > ThresholdMax)
        {
            errors["strongMatchThreshold"] = $"Threshold must be between {ThresholdMin} and {ThresholdMax}.";
        }

        return errors;
    }
}
