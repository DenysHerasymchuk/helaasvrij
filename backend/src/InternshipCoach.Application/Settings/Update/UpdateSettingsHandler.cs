using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Settings;

namespace InternshipCoach.Application.Settings.Update;

public sealed class UpdateSettingsHandler(ISettingsRepository settings, ICurrentUser currentUser)
{
    public async Task<SettingsResult> HandleAsync(UpdateSettingsCommand command, CancellationToken cancellationToken)
    {
        var errors = UserSettings.Validate(command.DefaultLanguage, command.StrongMatchThreshold);
        if (errors.Count > 0)
        {
            return SettingsResult.Invalid(errors);
        }

        var userId = currentUser.UserId;
        var existing = await settings.FindAsync(userId, cancellationToken);

        if (existing is null)
        {
            existing = UserSettings.Create(userId, command.DefaultLanguage, command.StrongMatchThreshold, command.EmailAlerts);
            await settings.AddAsync(existing, cancellationToken);
        }
        else
        {
            existing.Update(command.DefaultLanguage, command.StrongMatchThreshold, command.EmailAlerts);
            await settings.UpdateAsync(existing, cancellationToken);
        }

        return SettingsResult.From(existing);
    }
}
