using InternshipCoach.Application.Sprints;
using InternshipCoach.Domain.Sprints;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeSprintRepository : ISprintRepository
{
    public List<Sprint> Items { get; } = [];

    public Task<Sprint?> FindActiveAsync(CancellationToken ct) =>
        Task.FromResult(Items.FirstOrDefault(s => s.Status == SprintStatus.Active));

    public Task<Sprint?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(s => s.Id == id));

    public Task<IReadOnlyList<Sprint>> ListAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Sprint>>(Items.OrderByDescending(s => s.StartsOn).ToList());

    public Task AddAsync(Sprint sprint, CancellationToken ct)
    {
        Items.Add(sprint);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
}
