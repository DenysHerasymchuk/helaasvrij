using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Settings;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

public sealed class SettingsRepository(InternshipCoachDbContext db) : ISettingsRepository
{
    public Task<UserSettings?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        db.UserSettings.FirstOrDefaultAsync(settings => settings.UserId == userId, cancellationToken);

    public async Task AddAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        db.UserSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        db.UserSettings.Update(settings);
        await db.SaveChangesAsync(cancellationToken);
    }
}
