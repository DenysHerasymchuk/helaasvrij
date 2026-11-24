using InternshipCoach.Application.Abstractions;

namespace InternshipCoach.Application.Settings.Get;

public sealed class GetSettingsHandler(ISettingsRepository settings, ICurrentUser currentUser)
{
    public async Task<SettingsResult> HandleAsync(CancellationToken cancellationToken)
    {
        var existing = await settings.FindAsync(currentUser.UserId, cancellationToken);
        return existing is null ? SettingsResult.Defaults() : SettingsResult.From(existing);
    }
}
