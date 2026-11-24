using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Tests.Common;

public class CreatedAtTests
{
    [Fact]
    public void Stores_value_in_utc()
    {
        var local = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.FromHours(1));

        var createdAt = new CreatedAt(local);

        Assert.Equal(TimeSpan.Zero, createdAt.Value.Offset);
        Assert.Equal(local.UtcDateTime, createdAt.Value.UtcDateTime);
    }
}
