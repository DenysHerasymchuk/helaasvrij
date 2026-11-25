using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Postings;

// The match score of one posting at one moment, stored on every analyse and re-scan (feeds the score trend).
public sealed class ScoreSnapshot : IUserOwned
{
    public const int ReasonMaxLength = 40;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PostingId { get; private set; }
    public int Score { get; private set; }
    public string Reason { get; private set; } = null!;
    public DateTimeOffset TakenAt { get; private set; }

    private ScoreSnapshot()
    {
    }

    internal static ScoreSnapshot Create(Guid userId, Guid postingId, int score, string reason, DateTimeOffset takenAt)
    {
        if (score is < 0 or > 100)
        {
            throw new DomainException("Score must be between 0 and 100.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("Reason is required.");
        }

        var trimmed = reason.Trim();
        return new ScoreSnapshot
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PostingId = postingId,
            Score = score,
            Reason = trimmed.Length > ReasonMaxLength ? trimmed[..ReasonMaxLength] : trimmed,
            TakenAt = takenAt.ToUniversalTime()
        };
    }
}
