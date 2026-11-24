using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Applications;

public enum ApplicationStage
{
    Saved,
    Applied,
    Interview,
    Offer,
    Rejected
}

public static class ApplicationStages
{
    public static string ToCode(this ApplicationStage stage) => stage switch
    {
        ApplicationStage.Saved => "saved",
        ApplicationStage.Applied => "applied",
        ApplicationStage.Interview => "interview",
        ApplicationStage.Offer => "offer",
        _ => "rejected"
    };

    public static bool TryParse(string? code, out ApplicationStage stage)
    {
        foreach (var candidate in Enum.GetValues<ApplicationStage>())
        {
            if (string.Equals(candidate.ToCode(), code, StringComparison.OrdinalIgnoreCase))
            {
                stage = candidate;
                return true;
            }
        }

        stage = default;
        return false;
    }
}

// One student's application to one of their own postings. Named JobApplication because "Application" is also a layer namespace.
public sealed class JobApplication : IUserOwned
{
    public const int NotesMaxLength = 2000;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PostingId { get; private set; }
    public ApplicationStage Stage { get; private set; }
    public DateTimeOffset? AppliedAt { get; private set; }
    public DateTimeOffset StageChangedAt { get; private set; }
    public string Notes { get; private set; } = string.Empty;
    public CreatedAt CreatedAt { get; private set; } = null!;

    private JobApplication()
    {
    }

    public static JobApplication Create(Guid userId, Guid postingId, CreatedAt createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        if (postingId == Guid.Empty)
        {
            throw new DomainException("Posting id is required.");
        }

        return new JobApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PostingId = postingId,
            Stage = ApplicationStage.Saved,
            StageChangedAt = (createdAt ?? throw new DomainException("Creation time is required.")).Value,
            CreatedAt = createdAt
        };
    }

    /// <summary>Moves to another stage. Reaching Applied, Interview or Offer records the first time the student applied.</summary>
    public void MoveTo(ApplicationStage stage, DateTimeOffset now)
    {
        if (!Enum.IsDefined(stage))
        {
            throw new DomainException("Unknown stage.");
        }

        if (stage == Stage)
        {
            return;
        }

        Stage = stage;
        StageChangedAt = now.ToUniversalTime();
        if (AppliedAt is null && stage is ApplicationStage.Applied or ApplicationStage.Interview or ApplicationStage.Offer)
        {
            AppliedAt = StageChangedAt;
        }
    }

    public void SetNotes(string? notes)
    {
        var trimmed = notes?.Trim() ?? string.Empty;
        if (trimmed.Length > NotesMaxLength)
        {
            throw new DomainException($"Notes can be at most {NotesMaxLength} characters.");
        }

        Notes = trimmed;
    }
}
