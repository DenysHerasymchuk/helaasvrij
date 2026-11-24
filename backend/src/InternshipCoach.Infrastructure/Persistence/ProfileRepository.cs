using System.Text.Json;
using InternshipCoach.Application.Profile;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

// The global user filter on ProfileItem, ProfileBasics and Source scopes every query to the signed-in user.
public sealed class ProfileRepository(InternshipCoachDbContext db) : IProfileRepository
{
    // Sorted in memory: Npgsql cannot translate OrderBy on a record built by a constructor (crashed every analysis).
    public async Task<IReadOnlyList<ProfileDocument>> ListReadyDocumentsAsync(CancellationToken ct) =>
        (await db.SourceDocuments
            .Join(db.Sources.Where(source => source.Status == SourceStatus.Ready), document => document.SourceId, source => source.Id,
                (document, source) => new ProfileDocument(document.Id, source.Id, source.Kind, source.Label, document.Path, document.Text))
            .ToListAsync(ct))
            .OrderBy(document => document.SourceKind)
            .ThenBy(document => document.SourceLabel)
            .ThenBy(document => document.Path, StringComparer.Ordinal)
            .ToList();

    public async Task<IReadOnlyDictionary<Guid, DocumentRef>> GetDocumentRefsAsync(IReadOnlyCollection<Guid> documentIds, CancellationToken ct)
    {
        if (documentIds.Count == 0)
        {
            return new Dictionary<Guid, DocumentRef>();
        }

        var rows = await db.SourceDocuments
            .Where(document => documentIds.Contains(document.Id))
            .Join(db.Sources, document => document.SourceId, source => source.Id,
                (document, source) => new { document.Id, SourceId = source.Id, source.Kind, source.Label, document.Path, document.MetadataJson })
            .ToListAsync(ct);

        return rows.ToDictionary(
            row => row.Id,
            row => new DocumentRef(row.Id, row.SourceId, row.Kind, row.Label, row.Path, UrlFrom(row.MetadataJson)));
    }

    public async Task<IReadOnlyList<ProfileItem>> ListItemsAsync(CancellationToken ct) =>
        await db.ProfileItems.Include(item => item.Evidence).ToListAsync(ct);

    public Task<ProfileItem?> FindItemAsync(Guid id, CancellationToken ct) =>
        db.ProfileItems.Include(item => item.Evidence).FirstOrDefaultAsync(item => item.Id == id, ct);

    public void AddItem(ProfileItem item) => db.ProfileItems.Add(item);

    public async Task RemoveItemAsync(ProfileItem item, CancellationToken ct)
    {
        db.ProfileItems.Remove(item);
        await db.SaveChangesAsync(ct);
    }

    public Task<ProfileBasics?> FindBasicsAsync(CancellationToken ct) => db.ProfileBasics.FirstOrDefaultAsync(ct);

    public async Task AddBasicsAsync(ProfileBasics basics, CancellationToken ct)
    {
        db.ProfileBasics.Add(basics);
        await db.SaveChangesAsync(ct);
    }

    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    // GitHub documents carry {"repo":..., "url":...}. Only http(s) links are passed on.
    private static string? UrlFrom(string metadataJson)
    {
        try
        {
            using var json = JsonDocument.Parse(metadataJson);
            if (json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.TryGetProperty("url", out var url)
                && url.ValueKind == JsonValueKind.String
                && Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https")
            {
                return uri.ToString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }
}
