using InternshipCoach.Domain.Settings;

namespace InternshipCoach.Application.Abstractions;

public interface ISettingsRepository
{
    // Only the signed-in user's row is visible (query filter), null when never saved.
    Task<UserSettings?> FindAsync(Guid userId, CancellationToken cancellationToken);

    Task AddAsync(UserSettings settings, CancellationToken cancellationToken);

    Task UpdateAsync(UserSettings settings, CancellationToken cancellationToken);
}
