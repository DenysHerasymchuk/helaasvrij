using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Postings;

// One requirement of a posting. Coverage is added by the gap matrix feature.
public sealed class Requirement : IUserOwned
{
    public const int TextMaxLength = 300;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PostingId { get; private set; }
    public int Position { get; private set; }
    public string Text { get; private set; } = null!;
    public Importance Importance { get; private set; }

    private Requirement()
    {
    }

    internal static Requirement Create(Guid userId, Guid postingId, int position, string text, Importance importance)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("Requirement text is required.");
        }

        var trimmed = text.Trim();
        return new Requirement
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PostingId = postingId,
            Position = position,
            Text = trimmed.Length > TextMaxLength ? trimmed[..TextMaxLength] : trimmed,
            Importance = importance
        };
    }
}
