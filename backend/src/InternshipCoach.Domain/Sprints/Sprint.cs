using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Sprints;

public enum SprintStatus
{
    Active,
    Completed
}

// A search round: a time box with a goal. A user has at most one active sprint (the handler and a unique index enforce it).
public sealed class Sprint : IUserOwned
{
    public const int NameMaxLength = 80;
    public const int GoalMaxLength = 300;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Goal { get; private set; } = string.Empty;
    public DateOnly StartsOn { get; private set; }
    public DateOnly EndsOn { get; private set; }
    public SprintStatus Status { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public CreatedAt CreatedAt { get; private set; } = null!;

    private Sprint()
    {
    }

    public static Sprint Start(Guid userId, string? name, string? goal, DateOnly startsOn, DateOnly endsOn, CreatedAt createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length == 0)
        {
            throw new DomainException("Give the sprint a name.");
        }

        if (trimmedName.Length > NameMaxLength)
        {
            throw new DomainException($"The sprint name can be at most {NameMaxLength} characters.");
        }

        var trimmedGoal = goal?.Trim() ?? string.Empty;
        if (trimmedGoal.Length > GoalMaxLength)
        {
            throw new DomainException($"The goal can be at most {GoalMaxLength} characters.");
        }

        if (endsOn < startsOn)
        {
            throw new DomainException("The sprint must end on or after the day it starts.");
        }

        return new Sprint
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = trimmedName,
            Goal = trimmedGoal,
            StartsOn = startsOn,
            EndsOn = endsOn,
            Status = SprintStatus.Active,
            CreatedAt = createdAt ?? throw new DomainException("Creation time is required.")
        };
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status == SprintStatus.Completed)
        {
            throw new DomainException("This sprint is already completed.");
        }

        Status = SprintStatus.Completed;
        CompletedAt = now.ToUniversalTime();
    }
}
