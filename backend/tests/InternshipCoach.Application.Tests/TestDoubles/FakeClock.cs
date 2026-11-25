using InternshipCoach.Application.Abstractions;

namespace InternshipCoach.Application.Tests.TestDoubles;

// Fixed time for handler tests once use cases exist.
public sealed class FakeClock(DateTimeOffset utcNow) : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = utcNow;

    public void Set(DateTimeOffset utcNow) => UtcNow = utcNow;
}
