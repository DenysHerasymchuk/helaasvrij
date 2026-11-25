using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Profile;

// One claim about the student (a skill, project, experience, language or education). Every item has at least
// one verbatim quote from a source document; items without a source never get created.
public sealed class ProfileItem : IUserOwned
{
    public const int NameMaxLength = 200;

    private readonly List<ProfileEvidence> _evidence = [];

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public ProfileItemKind Kind { get; private set; }
    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;

    // The first quote and its document (the others are in Evidence).
    public string Quote { get; private set; } = null!;
    public Guid SourceDocumentId { get; private set; }
    public bool Confirmed { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public IReadOnlyCollection<ProfileEvidence> Evidence => _evidence;

    private ProfileItem()
    {
    }

    public static ProfileItem Create(
        Guid userId, ProfileItemKind kind, string name, Guid sourceDocumentId, string quote, CreatedAt createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Name is required.");
        }

        var trimmed = name.Trim();
        var item = new ProfileItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = kind,
            Name = trimmed.Length > NameMaxLength ? trimmed[..NameMaxLength] : trimmed,
            CreatedAt = createdAt.Value
        };
        item.NormalizedName = Normalise(item.Name);
        var first = ProfileEvidence.Create(item.Id, sourceDocumentId, quote);
        item.Quote = first.Quote;
        item.SourceDocumentId = sourceDocumentId;
        item._evidence.Add(first);
        return item;
    }

    /// <summary>Lower case, single spaces. Items with the same kind and normalised name are one item.</summary>
    public static string Normalise(string name) =>
        string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToLowerInvariant();

    /// <summary>Adds another quote. Returns false when the same quote from the same document is already stored.</summary>
    public bool AddEvidence(Guid sourceDocumentId, string quote)
    {
        var evidence = ProfileEvidence.Create(Id, sourceDocumentId, quote);

        if (_evidence.Any(e => e.SourceDocumentId == sourceDocumentId && e.Quote == evidence.Quote))
        {
            return false;
        }

        _evidence.Add(evidence);
        return true;
    }

    public void SetConfirmed(bool confirmed) => Confirmed = confirmed;
}
