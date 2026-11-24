using InternshipCoach.Application.Profile;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeProfileRepository(IEnumerable<Source>? sources = null) : IProfileRepository
{
    public List<Source> Sources { get; } = sources?.ToList() ?? [];
    public List<ProfileItem> Items { get; } = [];
    public ProfileBasics? Basics { get; private set; }
    public int Saves { get; private set; }

    public Task<IReadOnlyList<ProfileDocument>> ListReadyDocumentsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProfileDocument>>(Sources
            .Where(source => source.Status == SourceStatus.Ready)
            .SelectMany(source => source.Documents.Select(d => new ProfileDocument(d.Id, source.Id, source.Kind, source.Label, d.Path, d.Text)))
            .OrderBy(document => document.SourceKind)
            .ThenBy(document => document.Path)
            .ToList());

    public Task<IReadOnlyDictionary<Guid, DocumentRef>> GetDocumentRefsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, DocumentRef>>(Sources
            .SelectMany(source => source.Documents.Select(d => new DocumentRef(d.Id, source.Id, source.Kind, source.Label, d.Path, null)))
            .Where(reference => documentIds.Contains(reference.Id))
            .ToDictionary(reference => reference.Id));

    public Task<IReadOnlyList<ProfileItem>> ListItemsAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ProfileItem>>(Items.ToList());

    public Task<ProfileItem?> FindItemAsync(Guid id, CancellationToken ct) => Task.FromResult(Items.FirstOrDefault(item => item.Id == id));

    public void AddItem(ProfileItem item) => Items.Add(item);

    public Task RemoveItemAsync(ProfileItem item, CancellationToken ct)
    {
        Items.Remove(item);
        return Task.CompletedTask;
    }

    public Task<ProfileBasics?> FindBasicsAsync(CancellationToken ct) => Task.FromResult(Basics);

    public Task AddBasicsAsync(ProfileBasics basics, CancellationToken ct)
    {
        Basics = basics;
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken ct)
    {
        Saves++;
        return Task.CompletedTask;
    }
}
