using System.Globalization;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings;

public sealed record ExtractedRequirement(string Text, string Importance);

public sealed record ExtractedPosting(
    string Company, string Title, string? City, string Language, string? Deadline, List<ExtractedRequirement> Requirements);

/// <summary>Cheap model reads a posting (untrusted text, not personal data) and returns its facts, language and requirements.</summary>
public sealed class PostingExtractor(ILlmClient llm)
{
    public const string Feature = "posting-extract";
    public const int MaxPromptChars = 12000;

    private const string System =
        "You read one job or internship posting and extract its facts. " +
        "company and title: as written in the posting. city: the work location, or null. " +
        "language: the language the posting is written in, one of nl, fr, en. " +
        "deadline: the application deadline as YYYY-MM-DD, or null when none is stated. " +
        "requirements: the concrete skills, tools, knowledge, languages or qualifications asked of the candidate, " +
        "one short item each (max 12 words), in the posting's own language, at most 30. " +
        "For a spoken language requirement keep the level in the text when the posting states it, e.g. 'Nederlands (C1)'. " +
        "importance is 'required' by default and 'nice' only when the posting calls it a plus, bonus, asset, " +
        "nice to have, pluspunt, un atout or similar. Do not invent requirements and do not copy benefits or company descriptions.";

    private const string Schema = """
        {
          "type": "object",
          "required": ["company", "title", "language", "requirements"],
          "properties": {
            "company": { "type": "string", "minLength": 1 },
            "title": { "type": "string", "minLength": 1 },
            "city": { "type": ["string", "null"] },
            "language": { "enum": ["nl", "fr", "en"] },
            "deadline": { "type": ["string", "null"] },
            "requirements": {
              "type": "array",
              "maxItems": 30,
              "items": {
                "type": "object",
                "required": ["text", "importance"],
                "properties": {
                  "text": { "type": "string", "minLength": 1 },
                  "importance": { "enum": ["required", "nice"] }
                }
              }
            }
          }
        }
        """;

    public async Task<ExtractedPosting> ExtractAsync(Guid userId, string text, CancellationToken cancellationToken)
    {
        var clipped = text.Length > MaxPromptChars ? text[..MaxPromptChars] : text;
        var result = await llm.CompleteJsonAsync<ExtractedPosting>(
            new LlmCallContext(userId, Feature), LlmModel.Cheap, System,
            UntrustedInput.Wrap("posting", clipped), Schema, 2000, cancellationToken);
        return result.Value;
    }

    public static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParseExact(value?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    public static Importance ParseImportance(string value) =>
        string.Equals(value, "nice", StringComparison.OrdinalIgnoreCase) ? Importance.Nice : Importance.Required;
}
