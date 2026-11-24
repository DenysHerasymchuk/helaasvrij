using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Settings;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeSettingsRepository : ISettingsRepository
{
    public List<UserSettings> Rows { get; } = [];

    public Task<UserSettings?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Rows.FirstOrDefault(row => row.UserId == userId));

    public Task AddAsync(UserSettings settings, CancellationToken cancellationToken)
    {
        Rows.Add(settings);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(UserSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
}
