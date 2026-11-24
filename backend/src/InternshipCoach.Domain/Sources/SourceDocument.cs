using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Sources;

// One piece of raw text that evidence quotes point at (a README, a CV page, a site page).
public sealed class SourceDocument
{
    public const int PathMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid SourceId { get; private set; }
    public string Path { get; private set; } = null!;
    public string Text { get; private set; } = null!;
    public string MetadataJson { get; private set; } = "{}";

    private SourceDocument()
    {
    }

    internal static SourceDocument Create(Guid sourceId, string path, string text, string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new DomainException("Document path is required.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new DomainException("Document text is required.");
        }

        var trimmedPath = path.Trim();
        return new SourceDocument
        {
            Id = Guid.NewGuid(),
            SourceId = sourceId,
            Path = trimmedPath.Length > PathMaxLength ? trimmedPath[..PathMaxLength] : trimmedPath,
            Text = text,
            MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson
        };
    }
}
