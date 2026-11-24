using System.Text;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Analyse;

/// <summary>The checked result for one requirement. Code, not the model, decides what is kept.</summary>
public sealed record RequirementVerdict(
    Guid RequirementId, Coverage Coverage, string? Quote, Guid? DocumentId, string? Action, string? Project);

public sealed record GapRow(int Req, string? Coverage, string? Quote, string? Action, string? Project);

public sealed record GapReply(List<GapRow> Rows);

public sealed record ActionRow(int Req, string? Action, string? Project);

public sealed record ActionReply(List<ActionRow> Actions);

/// <summary>
/// Labels requirements against the student's confirmed evidence. The model only labels coverage and proposes
/// actions; code then drops evidence that is not one of the given quotes (and downgrades to None), rejects
/// actions that do not name one of the student's repos, regenerates those once, and otherwise leaves them empty.
/// Reusable for a single requirement: pass a list of one and freshly loaded evidence (re-scan).
/// </summary>
public sealed class GapAnalyser(ILlmClient llm, ICurrentUser currentUser)
{
    public const string Feature = "gap-analyse";

    /// <summary>Hard cap per run: one labelling call and at most one regeneration for bad actions.</summary>
    public const int MaxCalls = 2;

    private const int MaxTokens = 6000;

    private const string LabelSystem =
        "You compare the requirements of an internship posting with the evidence a student has confirmed. " +
        "Requirements are numbered in <untrusted_data name=\"requirements\">. The evidence is a numbered list of verbatim quotes " +
        "in <untrusted_data name=\"evidence\"> and the student's GitHub repository names are in <untrusted_data name=\"repos\">. " +
        "For every requirement return a row with: req (its number), coverage, quote, action and project. " +
        "coverage is strong (a quote clearly shows the student can do it), partial (a quote shows something related or weaker) " +
        "or none (no quote shows it). " +
        "quote must be copied character for character from one evidence quote, with no changes or joining; use null when coverage is none. " +
        "Never cite anything that is not in the evidence list. " +
        "For partial and none, action is one concrete step that closes the gap and project is the exact repository name it is done in; " +
        "the action text must contain that repository name. For strong, action and project are null. " +
        "Write actions in the language given in the first line of the user message.";

    private const string LabelSchema = """
        {
          "type": "object",
          "required": ["rows"],
          "properties": {
            "rows": {
              "type": "array",
              "maxItems": 30,
              "items": {
                "type": "object",
                "required": ["req", "coverage"],
                "properties": {
                  "req": { "type": "integer", "minimum": 1 },
                  "coverage": { "enum": ["strong", "partial", "none"] },
                  "quote": { "type": ["string", "null"] },
                  "action": { "type": ["string", "null"] },
                  "project": { "type": ["string", "null"] }
                }
              }
            }
          }
        }
        """;

    private const string ActionSystem =
        "A student has gaps against an internship posting. The gaps are numbered in <untrusted_data name=\"gaps\"> " +
        "and the student's GitHub repository names are in <untrusted_data name=\"repos\">. " +
        "For every gap return an action: one concrete step that closes it, done in one of those repositories. " +
        "project is the exact repository name and the action text must contain that repository name. " +
        "Write actions in the language given in the first line of the user message.";

    private const string ActionSchema = """
        {
          "type": "object",
          "required": ["actions"],
          "properties": {
            "actions": {
              "type": "array",
              "maxItems": 30,
              "items": {
                "type": "object",
                "required": ["req", "action", "project"],
                "properties": {
                  "req": { "type": "integer", "minimum": 1 },
                  "action": { "type": "string", "minLength": 1 },
                  "project": { "type": "string", "minLength": 1 }
                }
              }
            }
          }
        }
        """;

    public async Task<IReadOnlyList<RequirementVerdict>> AnalyseAsync(
        IReadOnlyList<Requirement> requirements, PostingLanguage language, GapEvidence evidence, CancellationToken ct)
    {
        if (requirements.Count == 0)
        {
            return [];
        }

        var context = new LlmCallContext(currentUser.UserId, Feature);
        var batch = requirements.Take(Posting.MaxRequirements).ToList();
        var languageLine = $"Output language for actions: {language.ToCode()}";

        var user = new StringBuilder(languageLine).Append("\n\n")
            .Append(UntrustedInput.Wrap("requirements", string.Join('\n', batch.Select((r, i) => $"{i + 1}. [{r.Importance.ToString().ToLowerInvariant()}] {r.Text}"))))
            .Append("\n\n")
            .Append(UntrustedInput.Wrap("evidence", string.Join('\n', evidence.Quotes.Select((q, i) => $"{i + 1}. ({q.Kind}: {q.ItemName}) \"{q.Quote}\""))))
            .Append("\n\n")
            .Append(UntrustedInput.Wrap("repos", string.Join('\n', evidence.Repos)))
            .ToString();

        var reply = (await llm.CompleteJsonAsync<GapReply>(context, LlmModel.Smart, LabelSystem, user, LabelSchema, MaxTokens, ct)).Value;

        var verdicts = new List<Draft>();
        for (var i = 0; i < batch.Count; i++)
        {
            var row = reply.Rows?.FirstOrDefault(r => r.Req == i + 1);
            verdicts.Add(Judge(batch[i], row, evidence));
        }

        // Gaps whose action is missing or does not name a real repo get one regeneration, then stay empty.
        if (evidence.Repos.Count > 0)
        {
            var open = verdicts.Where(v => v.Coverage != Coverage.Strong && v.Action is null).ToList();
            if (open.Count > 0)
            {
                var gaps = string.Join('\n', open.Select((v, i) => $"{i + 1}. {v.Requirement.Text} (coverage now: {v.Coverage.ToString().ToLowerInvariant()})"));
                var retryUser = new StringBuilder(languageLine).Append("\n\n")
                    .Append(UntrustedInput.Wrap("gaps", gaps)).Append("\n\n")
                    .Append(UntrustedInput.Wrap("repos", string.Join('\n', evidence.Repos)))
                    .ToString();

                var retry = (await llm.CompleteJsonAsync<ActionReply>(context, LlmModel.Smart, ActionSystem, retryUser, ActionSchema, MaxTokens, ct)).Value;
                for (var i = 0; i < open.Count; i++)
                {
                    var row = retry.Actions?.FirstOrDefault(a => a.Req == i + 1);
                    if (row is not null && TryNameRepo(row.Action, row.Project, evidence.Repos, out var action, out var project))
                    {
                        open[i].Action = action;
                        open[i].Project = project;
                    }
                }
            }
        }

        return verdicts.Select(v => new RequirementVerdict(v.Requirement.Id, v.Coverage, v.Quote, v.DocumentId, v.Action, v.Project)).ToList();
    }

    private static Draft Judge(Requirement requirement, GapRow? row, GapEvidence evidence)
    {
        var draft = new Draft(requirement);
        if (row is null)
        {
            return draft;
        }

        if (ScoreWeights.TryParse(row.Coverage, out var claimed) && claimed != Coverage.None)
        {
            // Evidence must be one of the given quotes and still pass the verbatim check against its own document.
            var match = evidence.Quotes.FirstOrDefault(q =>
                QuoteCheck.IsVerbatim(row.Quote, q.Quote)
                && evidence.DocumentTexts.TryGetValue(q.DocumentId, out var text)
                && QuoteCheck.IsVerbatim(row.Quote, text));
            if (match is not null)
            {
                draft.Coverage = claimed;
                draft.Quote = row.Quote!.Trim();
                draft.DocumentId = match.DocumentId;
            }
        }

        if (draft.Coverage != Coverage.Strong && TryNameRepo(row.Action, row.Project, evidence.Repos, out var action, out var project))
        {
            draft.Action = action;
            draft.Project = project;
        }

        return draft;
    }

    /// <summary>The project must be one of the student's repos and the action text must mention it.</summary>
    private static bool TryNameRepo(string? action, string? project, IReadOnlyList<string> repos, out string actionText, out string repo)
    {
        actionText = string.Empty;
        repo = string.Empty;
        if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(project))
        {
            return false;
        }

        var match = repos.FirstOrDefault(r => string.Equals(r, project.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is null || !action.Contains(match, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        actionText = action.Trim();
        repo = match;
        return true;
    }

    private sealed class Draft(Requirement requirement)
    {
        public Requirement Requirement { get; } = requirement;
        public Coverage Coverage { get; set; }
        public string? Quote { get; set; }
        public Guid? DocumentId { get; set; }
        public string? Action { get; set; }
        public string? Project { get; set; }
    }
}
