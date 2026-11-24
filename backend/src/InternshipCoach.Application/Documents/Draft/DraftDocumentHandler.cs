using System.Text.RegularExpressions;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Documents;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Documents.Draft;

/// <param name="Language">nl, fr or en; null = the posting's language.</param>
public sealed record DraftDocumentCommand(Guid PostingId, DocumentKind Kind, string? Language);

/// <summary>The draft could not be made from the student's evidence. Maps to HTTP 422.</summary>
public sealed class DraftRejectedException(string message) : Exception(message);

/// <summary>
/// Writes a cover letter or a recruiter message for one posting from the student's confirmed profile evidence and the
/// gaps they closed. The model must list the verbatim quotes it relied on; quotes that are not in the student's own
/// documents are dropped, and a draft with no verified quote or with numbers nobody supplied is rejected. At most 2 model calls.
/// </summary>
public sealed partial class DraftDocumentHandler(
    ILlmClient llm, IPostingRepository postings, GapEvidenceLoader loader, IDocumentRepository documents, ICurrentUser currentUser, IClock clock)
{
    public const int RecruiterMessageMaxLength = 300;
    public const int MaxAttempts = 2;
    public const string CoverLetterFeature = "cover-letter";
    public const string RecruiterMessageFeature = "recruiter-message";

    private const string Schema = """
        {
          "type": "object",
          "required": ["language", "text", "quotes"],
          "properties": {
            "language": { "type": "string", "enum": ["nl", "fr", "en"] },
            "text": { "type": "string", "minLength": 1 },
            "quotes": { "type": "array", "minItems": 1, "maxItems": 12, "items": { "type": "string" } }
          }
        }
        """;

    private static readonly Dictionary<string, string> LanguageNames = new() { ["nl"] = "Dutch", ["fr"] = "French", ["en"] = "English" };

    public async Task<Document?> HandleAsync(DraftDocumentCommand command, CancellationToken ct)
    {
        if (command.Kind is not (DocumentKind.CoverLetter or DocumentKind.RecruiterMessage))
        {
            throw new DraftRejectedException("Only a cover letter or a recruiter message can be drafted here.");
        }

        if (command.Language is not null && !Document.IsLanguage(command.Language))
        {
            throw new DraftRejectedException("Choose Dutch, French or English.");
        }

        var posting = await postings.FindAsync(command.PostingId, ct);
        if (posting is null)
        {
            return null;
        }

        var evidence = await loader.LoadAsync(ct);
        if (evidence.Quotes.Count == 0)
        {
            throw new GapInputException("Confirm at least one item on your profile first. Drafts use only confirmed items.");
        }

        var language = command.Language ?? posting.Language.ToCode();
        var recruiter = command.Kind == DocumentKind.RecruiterMessage;
        var system = BuildSystem(recruiter, language);
        var user = BuildUser(posting, evidence);
        var feature = recruiter ? RecruiterMessageFeature : CoverLetterFeature;

        var problem = string.Empty;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var prompt = problem.Length == 0 ? user : $"{user}\n\nYour previous draft was rejected: {problem} Write it again and fix that.";
            var reply = await llm.CompleteJsonAsync<Draft>(
                new LlmCallContext(currentUser.UserId, feature), LlmModel.Smart, system, prompt, Schema, LlmModels.SmartMinMaxTokens, ct);

            var text = Clean(reply.Value.Text);
            var verified = (reply.Value.Quotes ?? [])
                .Where(q => evidence.DocumentTexts.Values.Any(doc => QuoteCheck.IsVerbatim(q, doc)))
                .ToList();

            problem = Check(text, verified, posting, recruiter);
            if (problem.Length > 0)
            {
                continue;
            }

            var label = recruiter ? "Recruiter message" : "Cover letter";
            var title = $"{label} - {posting.Company}";
            var document = Document.Create(
                currentUser.UserId, command.Kind, language, title[..Math.Min(title.Length, Document.TitleMaxLength)], text, new CreatedAt(clock.UtcNow));
            await documents.AddAsync(document, ct);
            return document;
        }

        throw new DraftRejectedException($"The draft could not be checked against your evidence. {problem}");
    }

    private static string Check(string text, List<string> verifiedQuotes, Posting posting, bool recruiter)
    {
        if (text.Length == 0)
        {
            return "The text was empty.";
        }

        if (verifiedQuotes.Count == 0)
        {
            return "None of the quotes was copied exactly from the student's own documents.";
        }

        if (recruiter && text.Length > RecruiterMessageMaxLength)
        {
            return $"The message had {text.Length} characters; the limit is {RecruiterMessageMaxLength}.";
        }

        var allowed = Numbers(string.Join(' ', verifiedQuotes)) ;
        allowed.UnionWith(Numbers($"{posting.Company} {posting.Title} {posting.RawText}"));
        var invented = Numbers(text).Where(n => !allowed.Contains(n)).ToList();
        return invented.Count > 0 ? $"It used numbers that appear nowhere in the evidence or the posting: {string.Join(", ", invented)}." : string.Empty;
    }

    private static string BuildSystem(bool recruiter, string language)
    {
        var lang = LanguageNames[language];
        var form = recruiter
            ? $"Write a short message from the student to a recruiter at the company. At most {RecruiterMessageMaxLength - 20} characters including spaces, plain text, no greeting line break, no subject. It is never sent automatically; the student copies it."
            : "Write a cover letter of at most 250 words in 3 short paragraphs: why this company and role, the strongest matching evidence, and one closed gap or concrete next step. Plain text, paragraphs separated by a blank line, no placeholders, no address block.";
        return $"""
            You draft application text for a student looking for an internship in Belgium.
            {form}
            Write in {lang} and return "{language}" as language.
            Rules:
            - Use only facts that appear in the evidence quotes or the closed gaps. Never invent skills, tools, employers, dates, grades or numbers.
            - Name one of the student's own projects when the evidence supports it.
            - "quotes" lists the exact, character-for-character quotes from the evidence that support what you wrote. Copy them, do not paraphrase.
            - Do not claim a skill listed under open gaps.
            {UntrustedInput.SystemNotice}
            """;
    }

    private static string BuildUser(Posting posting, GapEvidence evidence)
    {
        var requirements = posting.Requirements.OrderBy(r => r.Position).ToList();
        var posted = $"Company: {posting.Company}\nRole: {posting.Title}\nCity: {posting.City}\nRequirements:\n" +
                     string.Join('\n', requirements.Select(r => $"- ({r.Importance}) {r.Text}"));
        var quotes = string.Join('\n', evidence.Quotes.Select(q => $"- [{q.Kind}] {q.ItemName}: \"{q.Quote}\""));
        var closed = requirements.Where(r => r.Done && r.Action is not null).ToList();
        var closedText = closed.Count == 0
            ? "(none)"
            : string.Join('\n', closed.Select(r => $"- {r.Text}: done in {r.ActionProject?.Replace("new:", "")} ({r.Action})"));
        var open = requirements.Where(r => !r.Done && r.Coverage != Coverage.Strong).Select(r => $"- {r.Text}");

        return $"{UntrustedInput.Wrap("posting", posted)}\n\n{UntrustedInput.Wrap("evidence_quotes", quotes)}\n\n" +
               $"{UntrustedInput.Wrap("closed_gaps", closedText)}\n\n{UntrustedInput.Wrap("open_gaps", string.Join('\n', open))}\n\n" +
               $"Student repos: {string.Join(", ", evidence.Repos)}";
    }

    private static string Clean(string? text) => text is null ? string.Empty : text.Replace("\r", string.Empty).Trim();

    private static HashSet<string> Numbers(string text) => DigitsRegex().Matches(text).Select(m => m.Value).ToHashSet();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();

    private sealed record Draft(string? Language, string? Text, List<string>? Quotes);
}
