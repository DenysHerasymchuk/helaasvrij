using InternshipCoach.Application.Profile;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Postings.Analyse;

/// <summary>One confirmed profile quote the model may cite, with the document it was found in.</summary>
public sealed record EvidenceQuote(Guid DocumentId, string ItemName, string Kind, string Quote);

/// <summary>What the model may see about the student: confirmed quotes (already checked against their documents) and repo names.</summary>
public sealed record GapEvidence(IReadOnlyList<EvidenceQuote> Quotes, IReadOnlyList<string> Repos, IReadOnlyDictionary<Guid, string> DocumentTexts);

/// <summary>Builds the evidence from the student's current data, so a re-scan after a fresh sync sees the new text.</summary>
public sealed class GapEvidenceLoader(IProfileRepository profile)
{
    public const int MaxQuotes = 120;
    public const int MaxRepos = 30;

    public async Task<GapEvidence> LoadAsync(CancellationToken ct)
    {
        var documents = await profile.ListReadyDocumentsAsync(ct);
        var texts = documents.ToDictionary(d => d.Id, d => d.Text);

        var quotes = (await profile.ListItemsAsync(ct))
            .Where(item => item.Confirmed)
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .SelectMany(item => item.Evidence
                .Where(e => texts.ContainsKey(e.SourceDocumentId))
                .Select(e => new EvidenceQuote(e.SourceDocumentId, item.Name, item.Kind.ToString().ToLowerInvariant(), e.Quote)))
            .Take(MaxQuotes)
            .ToList();

        var repos = documents
            .Where(d => d.SourceKind == SourceKind.GitHub && d.Path.Contains('/'))
            .Select(d => d.Path[..d.Path.IndexOf('/')])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .Take(MaxRepos)
            .ToList();

        return new GapEvidence(quotes, repos, texts);
    }
}
