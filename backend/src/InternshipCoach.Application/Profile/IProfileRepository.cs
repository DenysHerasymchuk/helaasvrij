using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Profile;

/// <summary>A source document with its text, used to extract and verify claims.</summary>
public sealed record ProfileDocument(Guid Id, Guid SourceId, SourceKind SourceKind, string SourceLabel, string Path, string Text);

/// <summary>Where a quote came from, without the text. Url is set when the document has a public page (a repo).</summary>
public sealed record DocumentRef(Guid Id, Guid SourceId, SourceKind SourceKind, string SourceLabel, string Path, string? Url);

// Every method is scoped to the signed-in user by the data layer.
public interface IProfileRepository
{
    /// <summary>Documents of every Ready source, in a stable order.</summary>
    Task<IReadOnlyList<ProfileDocument>> ListReadyDocumentsAsync(CancellationToken ct);

    Task<IReadOnlyDictionary<Guid, DocumentRef>> GetDocumentRefsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken ct);

    /// <summary>Items with their evidence, tracked for changes.</summary>
    Task<IReadOnlyList<ProfileItem>> ListItemsAsync(CancellationToken ct);

    Task<ProfileItem?> FindItemAsync(Guid id, CancellationToken ct);

    void AddItem(ProfileItem item);

    Task RemoveItemAsync(ProfileItem item, CancellationToken ct);

    Task SaveAsync(CancellationToken ct);
}
