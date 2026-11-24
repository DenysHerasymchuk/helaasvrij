using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Profile.Extract;

public sealed record ExtractionResult(int Created, int Merged, int Dropped, int Documents, int SkippedDocuments);

public sealed record ExtractedItem(int Doc, string Kind, string Name, string? Level, string Quote);

public sealed record ExtractedItems(List<ExtractedItem> Items);

/// <summary>
/// Turns Ready source documents into profile items. Each item carries a quote that code checks against the
/// document it points at; items that fail are dropped and counted. Source text is always wrapped as untrusted.
/// </summary>
public sealed class ExtractProfileHandler(IProfileRepository repository, ILlmClient llm, ICurrentUser currentUser, IClock clock)
{
    public const string Feature = "profile-extract";
    public const int MaxDocumentChars = 12_000;
    public const int MaxBatchChars = 14_000;
    public const int MaxDocumentsPerCall = 8;
    public const int MaxCalls = 12;
    public const int MaxItemsPerCall = 60;

    private const string System =
        "You read documents about one student (a CV, a LinkedIn export, a website, pasted text, or GitHub repositories) " +
        "and list what they show about the student. " +
        "Each document is given as <untrusted_data name=\"docN\"> where N is its number. " +
        "For every item return: doc (the number N of the document the quote comes from), " +
        "kind (skill, project, experience, language or education), name (short, e.g. 'Docker', 'weather-app', " +
        "'Intern at Acme', 'Dutch', 'Bachelor Applied Computer Science'), " +
        "level (only for kind language: the CEFR level A1 to C2 when the document states or clearly implies it, otherwise null), " +
        "and quote. The quote must be copied character for character from that document, 3 to 300 characters, " +
        "with no changes, no ellipsis and no joining of separate passages. " +
        "Only list what a quote proves. Do not invent or infer skills that are not written down. " +
        "For GitHub repository summaries, list each repository as a project and the languages and tools it names as skills. " +
        "At most 60 items.";

    private const string Schema = """
        {
          "type": "object",
          "required": ["items"],
          "properties": {
            "items": {
              "type": "array",
              "maxItems": 60,
              "items": {
                "type": "object",
                "required": ["doc", "kind", "name", "quote"],
                "properties": {
                  "doc": { "type": "integer", "minimum": 1 },
                  "kind": { "enum": ["skill", "project", "experience", "language", "education"] },
                  "name": { "type": "string", "minLength": 1 },
                  "level": { "type": ["string", "null"] },
                  "quote": { "type": "string", "minLength": 1 }
                }
              }
            }
          }
        }
        """;

    public async Task<ExtractionResult> HandleAsync(CancellationToken ct)
    {
        var documents = await repository.ListReadyDocumentsAsync(ct);
        if (documents.Count == 0)
        {
            throw new ProfileInputException("Add a source first (GitHub, a CV, your website or pasted text).");
        }

        var batches = BuildBatches(documents);
        var existing = (await repository.ListItemsAsync(ct)).ToDictionary(item => (item.Kind, item.NormalizedName));
        var created = 0;
        var merged = 0;
        var dropped = 0;
        var read = 0;

        foreach (var batch in batches.Take(MaxCalls))
        {
            var user = string.Join("\n\n", batch.Documents.Select((document, index) =>
                UntrustedInput.Wrap($"doc{index + 1}", Clip(document.Text))));

            var result = await llm.CompleteJsonAsync<ExtractedItems>(
                new LlmCallContext(currentUser.UserId, Feature), batch.Model, System, user, Schema, 4000, ct);
            read += batch.Documents.Count;

            foreach (var extracted in result.Value.Items.Take(MaxItemsPerCall))
            {
                if (!TryAccept(extracted, batch.Documents, out var document, out var kind))
                {
                    dropped++;
                    continue;
                }

                var key = (kind, ProfileItem.Normalise(extracted.Name));
                if (existing.TryGetValue(key, out var item))
                {
                    if (item.AddEvidence(document.Id, extracted.Quote, extracted.Level))
                    {
                        merged++;
                    }
                }
                else
                {
                    item = ProfileItem.Create(
                        currentUser.UserId, kind, extracted.Name, extracted.Level, document.Id, extracted.Quote, new CreatedAt(clock.UtcNow));
                    existing[key] = item;
                    repository.AddItem(item);
                    created++;
                }
            }

            // Save per call so a later failure does not throw away work that was already paid for.
            await repository.SaveAsync(ct);
        }

        return new ExtractionResult(created, merged, dropped, read, documents.Count - read);
    }

    private static bool TryAccept(ExtractedItem extracted, IReadOnlyList<ProfileDocument> documents, out ProfileDocument document, out ProfileItemKind kind)
    {
        document = null!;
        kind = default;

        if (extracted.Doc < 1 || extracted.Doc > documents.Count
            || string.IsNullOrWhiteSpace(extracted.Name)
            || !Enum.TryParse(extracted.Kind, ignoreCase: true, out kind))
        {
            return false;
        }

        document = documents[extracted.Doc - 1];
        return QuoteCheck.IsVerbatim(extracted.Quote, document.Text);
    }

    private static string Clip(string text) => text.Length > MaxDocumentChars ? text[..MaxDocumentChars] : text;

    // GitHub summaries are the only text the cheap model may see; CVs, sites and pasted text are personal.
    private static LlmModel ModelFor(SourceKind kind) => kind == SourceKind.GitHub ? LlmModel.Cheap : LlmModel.Smart;

    private static List<Batch> BuildBatches(IReadOnlyList<ProfileDocument> documents)
    {
        var batches = new List<Batch>();
        foreach (var group in documents.GroupBy(document => ModelFor(document.SourceKind)).OrderByDescending(g => g.Key))
        {
            var current = new List<ProfileDocument>();
            var chars = 0;
            foreach (var document in group)
            {
                var length = Math.Min(document.Text.Length, MaxDocumentChars);
                if (current.Count > 0 && (chars + length > MaxBatchChars || current.Count >= MaxDocumentsPerCall))
                {
                    batches.Add(new Batch(group.Key, current));
                    current = [];
                    chars = 0;
                }

                current.Add(document);
                chars += length;
            }

            if (current.Count > 0)
            {
                batches.Add(new Batch(group.Key, current));
            }
        }

        return batches;
    }

    private sealed record Batch(LlmModel Model, List<ProfileDocument> Documents);
}
