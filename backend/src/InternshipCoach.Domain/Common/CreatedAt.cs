namespace InternshipCoach.Domain.Common;

public sealed class CreatedAt
{
    public DateTimeOffset Value { get; }

    public CreatedAt(DateTimeOffset value)
    {
        Value = value.ToUniversalTime();
    }
}
