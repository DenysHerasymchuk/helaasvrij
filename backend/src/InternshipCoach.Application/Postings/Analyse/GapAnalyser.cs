using System.Text;
using System.Text.RegularExpressions;
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
/// A student without repos gets a small new-project idea instead (kebab-case name stored as "new:name", the action
/// mentions that name and the requirement's skill), with the same regenerate-once rule.
/// Reusable for a single requirement: pass a list of one and freshly loaded evidence (re-scan).
/// </summary>
public sealed class GapAnalyser(ILlmClient llm, ICurrentUser currentUser)
{
    public const string Feature = "gap-analyse";

    /// <summary>Hard cap per run: one labelling call and at most one regeneration for bad actions.</summary>
    public const int MaxCalls = 2;

    private const int MaxTokens = 6000;

    /// <summary>Stored before the project name of a new-project action, so re-scan and UI know there is no repo yet.</summary>
    public const string NewProjectPrefix = "new:";

    public static bool IsNewProject(string? project) => project?.StartsWith(NewProjectPrefix, StringComparison.Ordinal) == true;

    private const int NewProjectMaxLength = 40;

    private static readonly Regex KebabCase = new("^[a-z0-9]+(?:-[a-z0-9]+)+$", RegexOptions.CultureInvariant);

    // Words that say nothing about the skill itself, so an action that only repeats them stays vague (EN, NL, FR).
    private static readonly HashSet<string> Filler = new(StringComparer.OrdinalIgnoreCase)
    {
        "experience", "knowledge", "with", "and", "or", "of", "the", "in", "a", "an", "to", "for", "on", "skills", "skill", "good",
        "strong", "basic", "solid", "understanding", "working", "ability", "familiar", "familiarity", "plus", "is", "are", "be",
        "years", "year", "van", "met", "en", "de", "het", "een", "kennis", "ervaring", "op", "goede", "la", "le", "les", "des",
        "du", "et", "avec", "connaissance", "connaissances", "expérience", "bonne", "maîtrise",
    };

    private const string RepoActionRule =
        "For partial and none, action is one concrete step that closes the gap and project is the exact repository name it is done in; " +
        "the action text must contain that repository name. ";

    private const string NewProjectActionRule =
        "The student has no GitHub repositories yet. For partial and none, action is one small new project that closes the gap: " +
        "project is a short kebab-case project name (for example expense-tracker-api) and the action text is " +
        "\"Start `<project>`: <one specific step>\" that names the requirement's skill " +
        "(for example: Start `expense-tracker-api`: build a REST API with ASP.NET Core and add xUnit tests). ";

    private const string LabelSystem =
        "You compare the requirements of an internship posting with the evidence a student has confirmed. " +
        "Requirements are numbered in <untrusted_data name=\"requirements\">. The evidence is a numbered list of verbatim quotes " +
        "in <untrusted_data name=\"evidence\"> and the student's GitHub repository names are in <untrusted_data name=\"repos\">. " +
        "For every requirement return a row with: req (its number), coverage, quote, action and project. " +
        "coverage is strong (a quote clearly shows the student can do it), partial (a quote shows something related or weaker) " +
        "or none (no quote shows it). " +
        "quote must be copied character for character from one evidence quote, with no changes or joining; use null when coverage is none. " +
        "Never cite anything that is not in the evidence list. " +
        "{0}For strong, action and project are null. " +
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

    private const string NewProjectActionSystem =
        "A student has gaps against an internship posting. The gaps are numbered in <untrusted_data name=\"gaps\">. " +
        "The student has no GitHub repositories yet. For every gap return a small new project that closes it: " +
        "project is a short kebab-case project name (for example expense-tracker-api) and the action text is " +
        "\"Start `<project>`: <one specific step>\" that names the gap's skill. " +
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

        var hasRepos = evidence.Repos.Count > 0;
        var labelSystem = string.Format(LabelSystem, hasRepos ? RepoActionRule : NewProjectActionRule);
        var reply = (await llm.CompleteJsonAsync<GapReply>(context, LlmModel.Smart, labelSystem, user, LabelSchema, MaxTokens, ct)).Value;

        var verdicts = new List<Draft>();
        for (var i = 0; i < batch.Count; i++)
        {
            var row = reply.Rows?.FirstOrDefault(r => r.Req == i + 1);
            verdicts.Add(Judge(batch[i], row, evidence));
        }

        // Gaps whose action is missing or fails the check get one regeneration, then stay empty.
        var open = verdicts.Where(v => v.Coverage != Coverage.Strong && v.Action is null).ToList();
        if (open.Count > 0)
        {
            var gaps = string.Join('\n', open.Select((v, i) => $"{i + 1}. {v.Requirement.Text} (coverage now: {v.Coverage.ToString().ToLowerInvariant()})"));
            var retryUser = new StringBuilder(languageLine).Append("\n\n").Append(UntrustedInput.Wrap("gaps", gaps));
            if (hasRepos)
            {
                retryUser.Append("\n\n").Append(UntrustedInput.Wrap("repos", string.Join('\n', evidence.Repos)));
            }

            var retry = (await llm.CompleteJsonAsync<ActionReply>(
                context, LlmModel.Smart, hasRepos ? ActionSystem : NewProjectActionSystem, retryUser.ToString(), ActionSchema, MaxTokens, ct)).Value;
            for (var i = 0; i < open.Count; i++)
            {
                var row = retry.Actions?.FirstOrDefault(a => a.Req == i + 1);
                if (row is not null && TryAcceptAction(row.Action, row.Project, open[i].Requirement, evidence.Repos, out var action, out var project))
                {
                    open[i].Action = action;
                    open[i].Project = project;
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

        if (draft.Coverage != Coverage.Strong && TryAcceptAction(row.Action, row.Project, requirement, evidence.Repos, out var action, out var project))
        {
            draft.Action = action;
            draft.Project = project;
        }

        return draft;
    }

    /// <summary>With repos: the repo rule. Without: a kebab-case new project the action names, plus the requirement's skill.</summary>
    private static bool TryAcceptAction(
        string? action, string? project, Requirement requirement, IReadOnlyList<string> repos, out string actionText, out string stored) =>
        repos.Count > 0
            ? TryNameRepo(action, project, repos, out actionText, out stored)
            : TryNewProject(action, project, requirement.Text, out actionText, out stored);

    private static bool TryNewProject(string? action, string? project, string requirement, out string actionText, out string stored)
    {
        actionText = string.Empty;
        stored = string.Empty;
        var name = project?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(action) || name.Length > NewProjectMaxLength || !KebabCase.IsMatch(name)
            || !action.Contains(name, StringComparison.Ordinal)
            || !MentionsSkill(action.Replace(name, " ", StringComparison.Ordinal), requirement))
        {
            return false;
        }

        actionText = action.Trim();
        stored = NewProjectPrefix + name;
        return true;
    }

    /// <summary>The action must contain a non-filler word of the requirement as a whole word ("Docker", "C#", "xUnit").</summary>
    private static bool MentionsSkill(string action, string requirement)
    {
        var words = Regex.Split(requirement, @"[^\p{L}\p{N}#+.]+")
            .Select(w => w.Trim('.'))
            .Where(w => w.Length >= 2 && !Filler.Contains(w))
            .ToList();
        return words.Count == 0 || words.Any(w =>
            Regex.IsMatch(action, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(w)}(?![\p{{L}}\p{{N}}#+])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
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
