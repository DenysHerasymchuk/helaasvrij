using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Profile;

// One verbatim quote that backs a profile item, pointing at the document it was found in.
public sealed class ProfileEvidence
{
    public const int QuoteMaxLength = 600;

    public Guid Id { get; private set; }
    public Guid ProfileItemId { get; private set; }
    public Guid SourceDocumentId { get; private set; }
    public string Quote { get; private set; } = null!;

    private ProfileEvidence()
    {
    }

    internal static ProfileEvidence Create(Guid itemId, Guid sourceDocumentId, string quote)
    {
        if (sourceDocumentId == Guid.Empty)
        {
            throw new DomainException("Evidence needs a source document.");
        }

        if (string.IsNullOrWhiteSpace(quote))
        {
            throw new DomainException("Evidence needs a quote.");
        }

        var trimmed = quote.Trim();
        return new ProfileEvidence
        {
            Id = Guid.NewGuid(),
            ProfileItemId = itemId,
            SourceDocumentId = sourceDocumentId,
            Quote = trimmed.Length > QuoteMaxLength ? trimmed[..QuoteMaxLength] : trimmed
        };
    }
}
