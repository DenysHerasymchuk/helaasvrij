using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Postings;

// One requirement of a posting, with the gap matrix result once the posting has been analysed.
public sealed class Requirement : IUserOwned
{
    public const int TextMaxLength = 300;
    public const int QuoteMaxLength = 600;
    public const int ActionMaxLength = 500;
    public const int ProjectMaxLength = 200;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PostingId { get; private set; }
    public int Position { get; private set; }
    public string Text { get; private set; } = null!;
    public Importance Importance { get; private set; }
    public Coverage Coverage { get; private set; }
    public string? EvidenceQuote { get; private set; }
    public Guid? EvidenceSourceDocumentId { get; private set; }
    public string? Action { get; private set; }
    public string? ActionProject { get; private set; }
    public DateOnly? DueDate { get; private set; }
    public bool Done { get; private set; }
    public DateTimeOffset? AnalysedAt { get; private set; }

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

    /// <summary>
    /// Stores the gap matrix result. Evidence only counts together with its source document, and a requirement
    /// with no evidence can never be Partial or Strong.
    /// </summary>
    public void RecordAnalysis(
        Coverage coverage, string? evidenceQuote, Guid? evidenceSourceDocumentId, string? action, string? actionProject,
        DateOnly? dueDate, DateTimeOffset analysedAt)
    {
        var hasEvidence = !string.IsNullOrWhiteSpace(evidenceQuote) && evidenceSourceDocumentId is not null;
        if (coverage != Coverage.None && !hasEvidence)
        {
            throw new DomainException("Partial or strong coverage needs an evidence quote and its source.");
        }

        Coverage = coverage;
        EvidenceQuote = hasEvidence ? Clip(evidenceQuote!.Trim(), QuoteMaxLength) : null;
        EvidenceSourceDocumentId = hasEvidence ? evidenceSourceDocumentId : null;

        var hasAction = !string.IsNullOrWhiteSpace(action) && !string.IsNullOrWhiteSpace(actionProject);
        Action = hasAction ? Clip(action!.Trim(), ActionMaxLength) : null;
        ActionProject = hasAction ? Clip(actionProject!.Trim(), ProjectMaxLength) : null;
        DueDate = dueDate;
        Done = false;
        AnalysedAt = analysedAt.ToUniversalTime();
    }

    /// <summary>The student ticked the gap. A re-analysis clears the tick, so tick again after one.</summary>
    public void MarkDone() => Done = true;

    public void Undo() => Done = false;

    private static string Clip(string value, int max) => value.Length > max ? value[..max] : value;
}
