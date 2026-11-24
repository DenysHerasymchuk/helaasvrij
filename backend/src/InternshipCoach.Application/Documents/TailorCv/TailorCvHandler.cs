using System.Text;
using System.Text.RegularExpressions;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Documents;

namespace InternshipCoach.Application.Documents.TailorCv;

public sealed record TailorCvResult(Document? Document, int DroppedCount, IReadOnlyDictionary<string, string[]>? Errors)
{
    public bool Succeeded => Document is not null;

    public static TailorCvResult Invalid(string field, string message) => new(null, 0, new Dictionary<string, string[]> { [field] = [message] });
}

// Rewrites a pasted CV for a pasted posting. Every bullet and skill must quote the CV verbatim; the rest is dropped and counted.
public sealed partial class TailorCvHandler(ILlmClient llm, IDocumentRepository documents, ICurrentUser currentUser, IClock clock)
{
    public const int MaxTextLength = 20_000;
    public const string Feature = "cv-tailor";

    private const string System = """
        You tailor a student's CV to one job posting.
        Rules:
        - Use only facts stated in the CV. Reorder and rephrase them; never add new facts, skills, tools, employers, dates or numbers.
        - Put what matches the posting first, both the sections and the bullets inside each section.
        - Every bullet and every skill has "quote": an exact, character-for-character substring of the CV that supports it. Copy it, do not paraphrase it.
        - Leave out anything you cannot support with such a quote.
        - Keep each bullet to one line. "title" is a short CV title (the student's name and target role if the CV gives them).
        - "summary" is two or three sentences, built only from facts in the CV.
        """;

    private const string Schema = """
        {
          "type": "object",
          "required": ["language", "title", "summary", "sections", "skills"],
          "properties": {
            "language": { "type": "string", "enum": ["nl", "fr", "en"] },
            "title": { "type": "string", "minLength": 1 },
            "summary": { "type": "string" },
            "sections": {
              "type": "array",
              "maxItems": 12,
              "items": {
                "type": "object",
                "required": ["heading", "bullets"],
                "properties": {
                  "heading": { "type": "string" },
                  "bullets": {
                    "type": "array",
                    "maxItems": 15,
                    "items": {
                      "type": "object",
                      "required": ["text", "quote"],
                      "properties": { "text": { "type": "string" }, "quote": { "type": "string" } }
                    }
                  }
                }
              }
            },
            "skills": {
              "type": "array",
              "maxItems": 40,
              "items": {
                "type": "object",
                "required": ["name", "quote"],
                "properties": { "name": { "type": "string" }, "quote": { "type": "string" } }
              }
            }
          }
        }
        """;

    private static readonly Dictionary<string, string> LanguageNames = new() { ["nl"] = "Dutch", ["fr"] = "French", ["en"] = "English" };
    private static readonly Dictionary<string, string> SkillsHeading = new() { ["nl"] = "Vaardigheden", ["fr"] = "Compétences", ["en"] = "Skills" };

    public async Task<TailorCvResult> HandleAsync(TailorCvCommand command, CancellationToken ct)
    {
        var cv = command.CvText?.Trim() ?? string.Empty;
        var posting = command.PostingText?.Trim() ?? string.Empty;

        if (cv.Length == 0)
        {
            return TailorCvResult.Invalid("cvText", "Paste your CV.");
        }

        if (cv.Length > MaxTextLength)
        {
            return TailorCvResult.Invalid("cvText", $"Your CV can be at most {MaxTextLength} characters.");
        }

        if (posting.Length == 0)
        {
            return TailorCvResult.Invalid("postingText", "Paste the job posting.");
        }

        if (posting.Length > MaxTextLength)
        {
            return TailorCvResult.Invalid("postingText", $"The job posting can be at most {MaxTextLength} characters.");
        }

        if (command.Language is not null && !Document.IsLanguage(command.Language))
        {
            return TailorCvResult.Invalid("language", "Choose Dutch, French or English.");
        }

        var languageRule = command.Language is null
            ? "Write the CV in the language of the job posting and return that language code."
            : $"Write the CV in {LanguageNames[command.Language]} and return \"{command.Language}\" as language.";
        var user = $"{languageRule}\n\n{UntrustedInput.Wrap("cv", cv)}\n\n{UntrustedInput.Wrap("job_posting", posting)}";

        var reply = await llm.CompleteJsonAsync<TailoredCv>(
            new LlmCallContext(currentUser.UserId, Feature), LlmModel.Smart, System, user, Schema, LlmModels.SmartMinMaxTokens, ct);

        var draft = reply.Value;
        var language = command.Language ?? draft.Language ?? "en";
        var cvNumbers = Numbers(cv);
        var dropped = 0;

        // A line survives only if its quote is really in the CV and it adds no number the CV does not have.
        bool Keep(string? text, string? quote)
        {
            var ok = !string.IsNullOrWhiteSpace(text) && QuoteCheck.IsVerbatim(quote, cv) && Numbers(text).IsSubsetOf(cvNumbers);
            if (!ok)
            {
                dropped++;
            }

            return ok;
        }

        var sections = (draft.Sections ?? [])
            .Select(section => (Heading: Line(section.Heading),
                Bullets: (section.Bullets ?? []).Where(b => Keep(b.Text, b.Quote)).Select(b => Line(b.Text)).ToList()))
            .Where(section => section.Bullets.Count > 0)
            .ToList();
        var skills = (draft.Skills ?? []).Where(s => Keep(s.Name, s.Quote)).Select(s => Line(s.Name)).Distinct().ToList();

        if (sections.Count == 0 && skills.Count == 0)
        {
            return TailorCvResult.Invalid("cvText", "No line of the result could be matched to your CV. Try again.");
        }

        var summary = Line(draft.Summary);
        if (summary.Length > 0 && !Numbers(summary).IsSubsetOf(cvNumbers))
        {
            summary = string.Empty;
            dropped++;
        }

        var title = Line(draft.Title);
        title = title.Length == 0 ? "Tailored CV" : title[..Math.Min(title.Length, Document.TitleMaxLength)];

        var markdown = new StringBuilder($"# {title}\n");
        if (summary.Length > 0)
        {
            markdown.Append($"\n{summary}\n");
        }

        foreach (var (heading, bullets) in sections)
        {
            markdown.Append($"\n## {(heading.Length == 0 ? "-" : heading)}\n\n");
            bullets.ForEach(bullet => markdown.Append($"- {bullet}\n"));
        }

        if (skills.Count > 0)
        {
            markdown.Append($"\n## {SkillsHeading[language]}\n\n{string.Join(", ", skills)}\n");
        }

        var document = Document.Create(currentUser.UserId, DocumentKind.Cv, language, title, markdown.ToString(), new CreatedAt(clock.UtcNow));
        await documents.AddAsync(document, ct);
        return new TailorCvResult(document, dropped, null);
    }

    // One line of plain text, so model output cannot open its own markdown block.
    private static string Line(string? text) => WhitespaceRegex().Replace(text ?? string.Empty, " ").Trim().TrimStart('#').Trim();

    private static HashSet<string> Numbers(string text) => DigitsRegex().Matches(text).Select(m => m.Value).ToHashSet();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();

    private sealed record TailoredCv(string? Language, string? Title, string? Summary, List<Section>? Sections, List<Skill>? Skills);

    private sealed record Section(string? Heading, List<Bullet>? Bullets);

    private sealed record Bullet(string? Text, string? Quote);

    private sealed record Skill(string? Name, string? Quote);
}
