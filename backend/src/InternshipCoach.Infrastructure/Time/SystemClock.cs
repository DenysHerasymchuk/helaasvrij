using InternshipCoach.Application.Abstractions;

namespace InternshipCoach.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
