using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Profile;

public sealed record EvidenceView(string Quote, Guid SourceId, SourceKind SourceKind, string SourceLabel, string DocumentPath, string? Url);

public sealed record ProfileItemView(
    Guid Id, ProfileItemKind Kind, string Name, string? Level, string Quote, bool Confirmed, IReadOnlyList<EvidenceView> Evidence);

public sealed record BasicsView(string? School, DateOnly? StageStart, DateOnly? StageEnd)
{
    public static BasicsView Empty { get; } = new(null, null, null);

    public static BasicsView From(ProfileBasics? basics) =>
        basics is null ? Empty : new BasicsView(basics.School, basics.StageStart, basics.StageEnd);
}

public sealed record ProfileView(BasicsView Basics, IReadOnlyList<ProfileItemView> Items);

public static class ProfileViewBuilder
{
    public static async Task<IReadOnlyList<ProfileItemView>> BuildItemsAsync(
        IProfileRepository repository, IReadOnlyList<ProfileItem> items, CancellationToken ct)
    {
        var ids = items.SelectMany(item => item.Evidence).Select(e => e.SourceDocumentId).Distinct().ToList();
        var refs = await repository.GetDocumentRefsAsync(ids, ct);

        return items
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => new ProfileItemView(
                item.Id, item.Kind, item.Name, item.Level, item.Quote, item.Confirmed,
                item.Evidence
                    .Where(e => refs.ContainsKey(e.SourceDocumentId))
                    .Select(e =>
                    {
                        var document = refs[e.SourceDocumentId];
                        return new EvidenceView(e.Quote, document.SourceId, document.SourceKind, document.SourceLabel, document.Path, document.Url);
                    })
                    .ToList()))
            .ToList();
    }
}
