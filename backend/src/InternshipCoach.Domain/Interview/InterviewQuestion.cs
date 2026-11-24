using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Interview;

public enum QuestionSource
{
    /// <summary>Built from an open gap of the posting.</summary>
    Gap,

    /// <summary>Built from one of the student's own repos.</summary>
    Repo
}

// A practice question generated once for one posting. Every question names one of the student's repos.
public sealed class InterviewQuestion : IUserOwned
{
    public const int TextMaxLength = 600;
    public const int ProjectMaxLength = 200;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PostingId { get; private set; }
    public QuestionSource Source { get; private set; }
    public string Text { get; private set; } = null!;
    public string Project { get; private set; } = null!;

    /// <summary>The gap this question comes from (no foreign key: a re-analysis may replace requirements).</summary>
    public Guid? RequirementId { get; private set; }

    public int Position { get; private set; }
    public CreatedAt CreatedAt { get; private set; } = null!;

    private InterviewQuestion()
    {
    }

    public static InterviewQuestion Create(
        Guid userId, Guid postingId, QuestionSource source, string? text, string? project, Guid? requirementId, int position, CreatedAt createdAt)
    {
        if (userId == Guid.Empty || postingId == Guid.Empty)
        {
            throw new DomainException("User and posting are required.");
        }

        if (!Enum.IsDefined(source))
        {
            throw new DomainException("Unknown question source.");
        }

        var trimmedText = text?.Trim() ?? string.Empty;
        if (trimmedText.Length is 0 or > TextMaxLength)
        {
            throw new DomainException($"Question must be 1 to {TextMaxLength} characters.");
        }

        var trimmedProject = project?.Trim() ?? string.Empty;
        if (trimmedProject.Length is 0 or > ProjectMaxLength)
        {
            throw new DomainException("A question must name one of the student's repos.");
        }

        return new InterviewQuestion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PostingId = postingId,
            Source = source,
            Text = trimmedText,
            Project = trimmedProject,
            RequirementId = requirementId,
            Position = position,
            CreatedAt = createdAt ?? throw new DomainException("Creation time is required.")
        };
    }
}
