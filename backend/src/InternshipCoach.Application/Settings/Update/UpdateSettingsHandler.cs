using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Writing;
using InternshipCoach.Domain.Settings;

namespace InternshipCoach.Application.Settings.Update;

public sealed class UpdateSettingsHandler(ISettingsRepository settings, ICurrentUser currentUser)
{
    public async Task<SettingsResult> HandleAsync(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        var errors = UserSettings.Validate(command.DefaultLanguage, command.StrongMatchThreshold);
        ValidateSkills(errors, "documentSkills", command.DocumentSkills);
        ValidateSkills(errors, "interviewSkills", command.InterviewSkills);
        if (errors.Count > 0)
        {
            return SettingsResult.Invalid(errors);
        }

        var userId = currentUser.UserId;
        var existing = await settings.FindAsync(userId, cancellationToken);

        if (existing is null)
        {
            existing = UserSettings.Create(userId, command.DefaultLanguage, command.StrongMatchThreshold, command.EmailAlerts);
            existing.SetWritingSkills(command.DocumentSkills, command.InterviewSkills);
            await settings.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.Update(command.DefaultLanguage, command.StrongMatchThreshold, command.EmailAlerts);
            existing.SetWritingSkills(command.DocumentSkills, command.InterviewSkills);
            await settings.UpdateAsync(existing, cancellationToken);
        }

        return SettingsResult.From(existing);
    }

    private static void ValidateSkills(Dictionary<string, string> errors, string field, IReadOnlyList<string>? ids)
    {
        if (ids is null)
        {
            return;
        }

        if (ids.FirstOrDefault(id => !WritingSkills.Exists(id)) is { } unknown)
        {
            errors[field] = $"Unknown writing skill: {unknown}.";
        }
        else if (ids.Distinct().Count() > WritingSkills.MaxPerArea)
        {
            errors[field] = $"Choose at most {WritingSkills.MaxPerArea} writing skills.";
        }
    }
}
