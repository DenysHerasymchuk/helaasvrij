using System.Text.Json;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Application.Llm;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Evaluation;

public sealed record GoldDocument(string Id, string Text);

public sealed record GoldEvidence(string Document, string Kind, string Item, string Quote);

public sealed record GoldPersona(List<string> Repos, List<GoldDocument> Documents, List<GoldEvidence> Evidence);

public sealed record GoldRequirement(string Text, Importance Importance, Coverage Expected);

public sealed record GoldCase(string Id, string Company, string Title, string Persona, PostingLanguage Language, List<GoldRequirement> Requirements);

public sealed record GoldFile(Dictionary<string, GoldPersona> Personas, List<GoldCase> Cases);

public static class GoldSet
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public static GoldFile Load(string path)
    {
        var file = JsonSerializer.Deserialize<GoldFile>(File.ReadAllText(path), Options)
            ?? throw new InvalidOperationException("Gold set is empty.");

        // A gold set with a broken quote would blame the model for our own typo.
        foreach (var persona in file.Personas.Values)
        {
            var text = string.Join(' ', persona.Documents.Select(d => d.Text));
            foreach (var e in persona.Evidence.Where(e => !QuoteCheck.IsVerbatim(e.Quote, text)))
            {
                throw new InvalidOperationException($"Gold evidence is not verbatim in its document: {e.Quote}");
            }
        }

        return file;
    }

    /// <summary>Document ids become stable guids so runs are comparable.</summary>
    public static GapEvidence BuildEvidence(GoldPersona persona)
    {
        var ids = persona.Documents.ToDictionary(d => d.Id, d => GuidFor(d.Id));
        var texts = persona.Documents.ToDictionary(d => ids[d.Id], d => d.Text);
        var quotes = persona.Evidence.Select(e => new EvidenceQuote(ids[e.Document], e.Item, e.Kind, e.Quote)).ToList();
        return new GapEvidence(quotes, persona.Repos, texts);
    }

    private static Guid GuidFor(string id) =>
        new(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(id)));
}
