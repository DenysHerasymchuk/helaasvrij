using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Profile;
using InternshipCoach.Application.Sources.GitHub;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Gaps;

/// <summary>
/// Brings one freshly fetched repo into the existing GitHub source without replacing the source. Profile items and
/// their evidence point at SourceDocuments and cascade-delete with them, so a document is never removed while
/// anything still points at it. Changed documents are added as new documents; the old copy is dropped only when
/// nothing references it.
/// </summary>
public static class GitHubRepoMerger
{
    /// <summary>Ids of the documents that were added (new or changed text).</summary>
    public static IReadOnlySet<Guid> Merge(Source source, GitHubRepoData fresh, IReadOnlySet<Guid> referencedDocumentIds)
    {
        var scratch = Source.Create(source.UserId, SourceKind.GitHub, source.Label);
        GitHubDocumentBuilder.AddDocuments(scratch, [fresh]);

        var added = new HashSet<Guid>();
        foreach (var document in scratch.Documents)
        {
            var existing = source.Documents.Where(d => d.Path == document.Path).ToList();
            if (existing.Any(d => d.Text == document.Text))
            {
                continue;
            }

            added.Add(source.AddDocument(document.Path, document.Text, document.MetadataJson).Id);
            foreach (var old in existing.Where(d => !referencedDocumentIds.Contains(d.Id)))
            {
                source.RemoveDocument(old.Id);
            }
        }

        return added;
    }

    /// <summary>Evidence quotes that no longer appear verbatim in the document they point at (expected: 0).</summary>
    public static int CountUnverifiable(IEnumerable<ProfileItem> items, IEnumerable<ProfileDocument> documents)
    {
        var texts = documents.ToDictionary(d => d.Id, d => d.Text);
        return items.SelectMany(i => i.Evidence)
            .Count(e => !texts.TryGetValue(e.SourceDocumentId, out var text) || !QuoteCheck.IsVerbatim(e.Quote, text));
    }
}
