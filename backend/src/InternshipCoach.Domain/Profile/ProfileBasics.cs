using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Profile;

// One row per user: school and the period the student can do a stage.
public sealed class ProfileBasics : IUserOwned
{
    public const int SchoolMaxLength = 200;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string? School { get; private set; }
    public DateOnly? StageStart { get; private set; }
    public DateOnly? StageEnd { get; private set; }

    private ProfileBasics()
    {
    }

    public static ProfileBasics Create(Guid userId, string? school, DateOnly? stageStart, DateOnly? stageEnd)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        var basics = new ProfileBasics { Id = Guid.NewGuid(), UserId = userId };
        basics.Update(school, stageStart, stageEnd);
        return basics;
    }

    public void Update(string? school, DateOnly? stageStart, DateOnly? stageEnd)
    {
        var errors = Validate(school, stageStart, stageEnd);
        if (errors.Count > 0)
        {
            throw new DomainException(string.Join(" ", errors.Values));
        }

        School = string.IsNullOrWhiteSpace(school) ? null : school.Trim();
        StageStart = stageStart;
        StageEnd = stageEnd;
    }

    /// <summary>Field name (camelCase) to message; empty when valid.</summary>
    public static Dictionary<string, string> Validate(string? school, DateOnly? stageStart, DateOnly? stageEnd)
    {
        var errors = new Dictionary<string, string>();

        if (school is not null && school.Trim().Length > SchoolMaxLength)
        {
            errors["school"] = $"School name is too long (max {SchoolMaxLength} characters).";
        }

        if (stageStart is not null && stageEnd is not null && stageEnd < stageStart)
        {
            errors["stageEnd"] = "The stage end date must be on or after the start date.";
        }

        return errors;
    }
}
