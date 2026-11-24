using InternshipCoach.Application.Abstractions;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeCurrentUser(Guid? userId = null) : ICurrentUser
{
    public Guid UserId { get; } = userId ?? Guid.NewGuid();
}
