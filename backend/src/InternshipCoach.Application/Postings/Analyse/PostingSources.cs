using InternshipCoach.Application.Profile;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Analyse;

/// <summary>Looks up where each evidence quote of a posting came from, so the page can link to the source.</summary>
public sealed class ResolveEvidenceSourcesHandler(IProfileRepository profile)
{
    public async Task<IReadOnlyDictionary<Guid, DocumentRef>> HandleAsync(Posting posting, CancellationToken ct)
    {
        var ids = posting.Requirements.Where(r => r.EvidenceSourceDocumentId is not null)
            .Select(r => r.EvidenceSourceDocumentId!.Value).Distinct().ToList();
        return await profile.GetDocumentRefsAsync(ids, ct);
    }
}
