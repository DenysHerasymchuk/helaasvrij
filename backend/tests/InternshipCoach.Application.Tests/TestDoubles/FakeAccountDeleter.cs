using InternshipCoach.Application.Abstractions;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeAccountDeleter : IAccountDeleter
{
    public List<Guid> Deleted { get; } = [];

    public Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        Deleted.Add(userId);
        return Task.CompletedTask;
    }
}
