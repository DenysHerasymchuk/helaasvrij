using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Sources;

// Raw material the profile is built from: a GitHub account, a CV, a website or pasted text.
public sealed class Source : IUserOwned
{
    public const int LabelMaxLength = 200;
    public const int ErrorMaxLength = 500;

    private readonly List<SourceDocument> _documents = [];

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public SourceKind Kind { get; private set; }
    public string Label { get; private set; } = null!;
    public SourceStatus Status { get; private set; }
    public DateTimeOffset? LastSyncedAt { get; private set; }
    public string? Error { get; private set; }
    public IReadOnlyCollection<SourceDocument> Documents => _documents;

    private Source()
    {
    }

    public static Source Create(Guid userId, SourceKind kind, string label)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainException("Label is required.");
        }

        var trimmed = label.Trim();
        return new Source
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = kind,
            Label = trimmed.Length > LabelMaxLength ? trimmed[..LabelMaxLength] : trimmed,
            Status = SourceStatus.Pending
        };
    }

    public SourceDocument AddDocument(string path, string text, string? metadataJson = null)
    {
        var document = SourceDocument.Create(Id, path, text, metadataJson);
        _documents.Add(document);
        return document;
    }

    public void ClearDocuments() => _documents.Clear();

    public void MarkReady(DateTimeOffset now)
    {
        if (_documents.Count == 0)
        {
            throw new DomainException("A ready source needs at least one document.");
        }

        Status = SourceStatus.Ready;
        LastSyncedAt = now.ToUniversalTime();
        Error = null;
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        var message = string.IsNullOrWhiteSpace(error) ? "Sync failed." : error.Trim();
        Status = SourceStatus.Failed;
        LastSyncedAt = now.ToUniversalTime();
        Error = message.Length > ErrorMaxLength ? message[..ErrorMaxLength] : message;
    }

    public void MarkPending()
    {
        Status = SourceStatus.Pending;
        Error = null;
    }
}
