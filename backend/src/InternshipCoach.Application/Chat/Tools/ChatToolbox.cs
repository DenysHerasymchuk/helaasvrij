using System.Text.Json;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Applications;
using InternshipCoach.Application.Gaps;
using InternshipCoach.Application.Journey;
using InternshipCoach.Application.Postings.Add;
using InternshipCoach.Application.Profile;
using InternshipCoach.Application.Sources;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Chat.Tools;

/// <summary>What a read tool hands back: trimmed data for the model and a short label for the trace ("3 repos").</summary>
public sealed record ToolOutput(object Data, string Summary);

/// <summary>An action the model proposed. Nothing runs until the student confirms it.</summary>
public sealed record PendingChatAction(string Tool, IReadOnlyDictionary<string, string> Args, string Summary);

public enum ChatActionStatus
{
    Ok,
    NotFound,
    Invalid
}

/// <param name="Undo">The action that reverses this one, when there is one. The student starts it with one click.</param>
public sealed record ChatActionResult(ChatActionStatus Status, string Message, string? NavigateTo = null, PendingChatAction? Undo = null);

/// <summary>A tool failed in a way the model may read (unknown posting, missing argument).</summary>
public sealed class ChatToolException(string message) : Exception(message);

/// <summary>
/// The tools the chat assistant may use. Every lookup goes through user-scoped repositories and handlers, so the
/// signed-in user's query filter applies. Read results are trimmed: names, levels, scores and quotes of at most
/// <see cref="MaxText"/> characters, never raw CV text or source documents.
/// </summary>
public sealed class ChatToolbox(
    IProfileRepository profile,
    ISourceRepository sources,
    IPostingRepository postings,
    IApplicationRepository applications,
    IJobSnapshot jobs,
    ListGapsHandler listGaps,
    GetJourneyHandler journey,
    CreateApplicationHandler createApplication,
    UpdateApplicationHandler updateApplication,
    AddPostingFromJobHandler saveJob,
    PlanningTools planning)
{
    public const int MaxText = 200;
    public const int MaxListItems = 15;

    private static readonly ToolParam PostingId = new("postingId", "id from list_postings");

    private static readonly IReadOnlyList<ToolSpec> CoreReadTools =
    [
        ToolSpec.Read("get_profile", "profile items (skills, projects, experience, education, certifications) and languages"),
        ToolSpec.Read("list_github_projects", "synced GitHub repos: name, main language, commits, Dockerfile/tests/CI, last sync"),
        ToolSpec.Read("list_postings", "saved postings: id, company, title, match score, stage on the applications board"),
        ToolSpec.Read("get_posting", "one posting's requirements, coverage, evidence and open gaps", PostingId),
        ToolSpec.Read("list_gaps", "open gap actions across all postings, biggest impact first (with requirementId)"),
        ToolSpec.Read("list_applications", "applications board: company, title, stage"),
        ToolSpec.Read("get_journey", "current step on the path to an application and the next action"),
        ToolSpec.Read("search_jobs", "Belgian internship ads from Adzuna", new ToolParam("what", "keywords", false), new ToolParam("where", "city", false))
    ];

    private static readonly IReadOnlyList<ToolSpec> CoreActionTools =
    [
        ToolSpec.Action("set_application_stage", "move a posting on the applications board",
            PostingId, new ToolParam("stage", "stage", true, ["saved", "applied", "interview", "offer", "rejected"])),
        ToolSpec.Action("save_job", "save an Adzuna ad from search_jobs as a posting", new ToolParam("jobId", "adzuna id")),
        ToolSpec.Action("add_to_applications", "put a saved posting on the applications board", PostingId)
    ];

    /// <summary>The catalog chat and the MCP server share: one list, one place to add a tool.</summary>
    public static readonly IReadOnlyList<ToolSpec> ReadTools = [.. CoreReadTools, .. PlanningTools.ReadSpecs];

    public static readonly IReadOnlyList<ToolSpec> ActionTools = [.. CoreActionTools, .. PlanningTools.ActionSpecs];

    public static bool IsReadTool(string name) => ReadTools.Any(t => t.Name == name);

    public static bool IsActionTool(string name) => ActionTools.Any(t => t.Name == name);

    public async Task<ToolOutput> RunReadAsync(string name, IReadOnlyDictionary<string, string> args, CancellationToken ct) => name switch
    {
        "get_profile" => await GetProfileAsync(ct),
        "list_github_projects" => await ListGitHubProjectsAsync(ct),
        "list_postings" => await ListPostingsAsync(ct),
        "get_posting" => await GetPostingAsync(args, ct),
        "list_gaps" => await ListGapsAsync(ct),
        "list_applications" => await ListApplicationsAsync(ct),
        "get_journey" => await GetJourneyAsync(ct),
        "search_jobs" => await SearchJobsAsync(args, ct),
        _ when PlanningTools.Handles(name) => await planning.RunReadAsync(name, args, ct),
        _ => throw new ChatToolException($"Unknown tool {Clip(name, 40)}.")
    };

    /// <summary>Checks an action the model wants to propose and words the question for the student. Runs nothing.</summary>
    public async Task<PendingChatAction> ProposeAsync(string tool, IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        switch (tool)
        {
            case "set_application_stage":
            {
                var posting = await FindPostingAsync(args, ct);
                var stage = ParseStage(args);
                return new PendingChatAction(tool, Only(args, "postingId", "stage"),
                    $"Move {posting.Company} to {Capitalize(stage.ToCode())}?");
            }
            case "add_to_applications":
            {
                var posting = await FindPostingAsync(args, ct);
                return new PendingChatAction(tool, Only(args, "postingId"), $"Add {posting.Company} ({Clip(posting.Title, 60)}) to applications?");
            }
            case "save_job":
            {
                var job = await FindJobAsync(args, ct);
                return new PendingChatAction(tool, Only(args, "jobId"),
                    $"Save {Clip(job.Title, 80)}{(string.IsNullOrWhiteSpace(job.Company) ? "" : $" at {Clip(job.Company, 60)}")} to your postings?");
            }
            default:
                return await planning.ProposeAsync(tool, args, ct);
        }
    }

    /// <summary>Runs a confirmed action after checking the tool, ownership and arguments again.</summary>
    public async Task<ChatActionResult> ExecuteAsync(string tool, IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        if (!IsActionTool(tool))
        {
            return new ChatActionResult(ChatActionStatus.Invalid, "This action is not allowed.");
        }

        try
        {
            switch (tool)
            {
                case not ("set_application_stage" or "add_to_applications" or "save_job"):
                    return await planning.ExecuteAsync(tool, args, ct);
                case "set_application_stage":
                {
                    var posting = await FindPostingAsync(args, ct);
                    var stage = ParseStage(args);
                    var id = await applications.FindIdForPostingAsync(posting.Id, ct);
                    // Only a card that already existed has a stage to go back to.
                    var previous = id is { } existing ? (await applications.FindAsync(existing, ct))?.Stage : null;
                    id ??= (await createApplication.HandleAsync(new CreateApplicationCommand(posting.Id), ct)).Card?.Application.Id;
                    if (id is null)
                    {
                        return new ChatActionResult(ChatActionStatus.NotFound, "Posting not found.");
                    }

                    var result = await updateApplication.HandleAsync(new UpdateApplicationCommand(id.Value, stage.ToCode(), null), ct);
                    return result.Outcome switch
                    {
                        ApplicationOutcome.Ok => new ChatActionResult(ChatActionStatus.Ok, $"Moved {posting.Company} to {Capitalize(stage.ToCode())}.", "/board",
                            previous is { } back && back != stage
                                ? new PendingChatAction(tool, new Dictionary<string, string> { ["postingId"] = posting.Id.ToString(), ["stage"] = back.ToCode() },
                                    $"Move {posting.Company} back to {Capitalize(back.ToCode())}")
                                : null),
                        ApplicationOutcome.NotFound => new ChatActionResult(ChatActionStatus.NotFound, "Application not found."),
                        _ => new ChatActionResult(ChatActionStatus.Invalid, result.Error ?? "Could not move it.")
                    };
                }
                case "add_to_applications":
                {
                    var posting = await FindPostingAsync(args, ct);
                    var result = await createApplication.HandleAsync(new CreateApplicationCommand(posting.Id), ct);
                    return result.Outcome == ApplicationOutcome.Ok
                        ? new ChatActionResult(ChatActionStatus.Ok, $"Added {posting.Company} to applications.", "/board")
                        : new ChatActionResult(ChatActionStatus.NotFound, "Posting not found.");
                }
                default: // save_job
                {
                    var outcome = await saveJob.HandleAsync(new AddPostingFromJobCommand(Arg(args, "jobId")), ct);
                    return outcome.Posting is { } saved
                        ? new ChatActionResult(ChatActionStatus.Ok,
                            outcome.Status == PostingStatus.Existing ? $"{saved.Company} was already saved." : $"Saved {saved.Company} to your postings.",
                            "/backlog")
                        : new ChatActionResult(outcome.Status == PostingStatus.NotFound ? ChatActionStatus.NotFound : ChatActionStatus.Invalid,
                            outcome.Message ?? "Could not save this job.");
                }
            }
        }
        catch (ChatToolException ex)
        {
            return new ChatActionResult(ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? ChatActionStatus.NotFound
                : ChatActionStatus.Invalid, ex.Message);
        }
    }

    private async Task<ToolOutput> GetProfileAsync(CancellationToken ct)
    {
        var items = await profile.ListItemsAsync(ct);
        var confirmed = items.Where(i => i.Confirmed).ToList();
        var data = new
        {
            languages = confirmed.Where(i => i.Kind == ProfileItemKind.Language).Select(i => new { name = Clip(i.Name) }),
            items = confirmed.Where(i => i.Kind != ProfileItemKind.Language)
                .OrderBy(i => i.Kind).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
                .Take(40)
                .Select(i => new { kind = i.Kind.ToString().ToLowerInvariant(), name = Clip(i.Name), quote = Clip(i.Quote) })
        };
        return new ToolOutput(data, $"{confirmed.Count} confirmed items");
    }

    private async Task<ToolOutput> ListGitHubProjectsAsync(CancellationToken ct)
    {
        var source = await sources.FindByKindAsync(SourceKind.GitHub, ct);
        if (source is null)
        {
            return new ToolOutput(new { connected = false, repos = Array.Empty<object>() }, "not connected");
        }

        var repos = source.Documents
            .Where(d => d.Path.EndsWith("/summary", StringComparison.Ordinal))
            .Select(d => RepoFromMetadata(d.Path, d.MetadataJson))
            .Take(30)
            .ToList();
        var data = new { connected = true, status = source.Status.ToString().ToLowerInvariant(), lastSync = source.LastSyncedAt, repos };
        return new ToolOutput(data, Count(repos.Count, "repo"));
    }

    private static object RepoFromMetadata(string path, string metadataJson)
    {
        var name = path[..path.LastIndexOf('/')];
        try
        {
            var meta = JsonDocument.Parse(metadataJson).RootElement;
            string? Text(string key) => meta.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            bool Flag(string key) => meta.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;
            int? Number(string key) => meta.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
            return new
            {
                name = Clip(name, 100), language = Text("language"), commits = Number("commits"), lastPush = Text("pushedAt"),
                dockerfile = Flag("hasDockerfile"), tests = Flag("hasTests"), ci = Flag("hasCi")
            };
        }
        catch (JsonException)
        {
            return new { name = Clip(name, 100) };
        }
    }

    private async Task<ToolOutput> ListPostingsAsync(CancellationToken ct)
    {
        var all = await postings.ListAsync(null, null, ct);
        var board = (await applications.ListAsync(ct)).ToDictionary(c => c.Application.PostingId, c => c.Application.Stage);
        var data = all.Take(30).Select(p => new
        {
            id = p.Id,
            company = Clip(p.Company, 100),
            title = Clip(p.Title, 100),
            city = Clip(p.City, 60),
            score = p.IsAnalysed ? p.Score() : (int?)null,
            boardStage = board.TryGetValue(p.Id, out var stage) ? stage.ToCode() : null,
            deadline = p.Deadline
        }).ToList();
        return new ToolOutput(new { postings = data }, $"{all.Count} saved");
    }

    private async Task<ToolOutput> GetPostingAsync(IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        var p = await FindPostingAsync(args, ct);
        var open = p.Requirements.Count(r => r.AnalysedAt is not null && r.Action is not null && !r.Done);
        var data = new
        {
            id = p.Id,
            company = Clip(p.Company, 100),
            title = Clip(p.Title, 100),
            city = Clip(p.City, 60),
            language = p.Language.ToString().ToLowerInvariant(),
            analysed = p.IsAnalysed,
            score = p.IsAnalysed ? p.Score() : (int?)null,
            deadline = p.Deadline,
            openGaps = open,
            requirements = p.Requirements.OrderBy(r => r.Position).Select(r => new
            {
                text = Clip(r.Text),
                importance = r.Importance.ToString().ToLowerInvariant(),
                coverage = r.AnalysedAt is null ? null : r.Coverage.ToString().ToLowerInvariant(),
                evidence = Clip(r.EvidenceQuote),
                action = Clip(r.Action),
                project = Clip(r.ActionProject, 100),
                done = r.Done
            })
        };
        return new ToolOutput(data, $"{Clip(p.Company, 40)} · {Count(open, "open gap")}");
    }

    private async Task<ToolOutput> ListGapsAsync(CancellationToken ct)
    {
        var gaps = await listGaps.HandleAsync(ct);
        var open = gaps.Where(g => !g.Done).ToList();
        var data = new
        {
            openCount = open.Count,
            doneCount = gaps.Count - open.Count,
            gaps = open.Take(MaxListItems).Select(g => new
            {
                requirementId = g.RequirementId,
                postingId = g.PostingId,
                company = Clip(g.Company, 100),
                requirement = Clip(g.Requirement),
                importance = g.Importance.ToString().ToLowerInvariant(),
                coverage = g.Coverage.ToString().ToLowerInvariant(),
                action = Clip(g.Action),
                project = Clip(g.Project, 100),
                scoreGain = g.ScoreGain,
                due = g.DueDate
            })
        };
        return new ToolOutput(data, $"{open.Count} open");
    }

    private async Task<ToolOutput> ListApplicationsAsync(CancellationToken ct)
    {
        var cards = await applications.ListAsync(ct);
        var data = cards.Take(30).Select(c => new
        {
            postingId = c.Application.PostingId,
            company = Clip(c.Company, 100),
            title = Clip(c.Title, 100),
            stage = c.Application.Stage.ToCode(),
            appliedAt = c.Application.AppliedAt,
            deadline = c.Deadline
        }).ToList();
        return new ToolOutput(new { applications = data }, Count(cards.Count, "application"));
    }

    private async Task<ToolOutput> GetJourneyAsync(CancellationToken ct)
    {
        var j = await journey.HandleAsync(ct);
        var data = new
        {
            currentStep = j.CurrentStep,
            steps = "1 build profile, 2 pick a posting, 3 close gaps, 4 apply and track",
            complete = j.Complete,
            nextAction = new { label = Clip(j.NextAction.Label, 100), href = j.NextAction.Href },
            bestPosting = j.BestPosting is null ? null : new { id = j.BestPosting.Id, company = Clip(j.BestPosting.Company, 100), score = j.BestPosting.Score },
            j.HasSources, j.ConfirmedItemCount, j.SavedPostingCount, j.OpenGapCount, j.AppliedCount
        };
        return new ToolOutput(data, $"step {j.CurrentStep} of 4");
    }

    // Same filter as JobsController.Search, on the shared Adzuna snapshot (public data).
    private async Task<ToolOutput> SearchJobsAsync(IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        var all = await jobs.LoadAsync(ct) ?? throw new ChatToolException("Job search is not set up right now.");
        var words = (args.GetValueOrDefault("what") ?? "").ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w is not ("stage" or "internship" or "stagiair" or "intern")).ToArray();
        var city = args.GetValueOrDefault("where")?.Trim().ToLowerInvariant();
        var hits = all
            .Where(j => words.All(w => $"{j.Title} {j.Description} {j.Company}".ToLowerInvariant().Contains(w)))
            .Where(j => string.IsNullOrEmpty(city) || (j.Location ?? "").ToLowerInvariant().Contains(city))
            .ToList();
        var data = new
        {
            total = hits.Count,
            jobs = hits.Take(8).Select(j => new
            {
                id = j.Id, title = Clip(j.Title, 120), company = Clip(j.Company, 100), location = Clip(j.Location, 80),
                posted = j.Created is { Length: >= 10 } c ? c[..10] : null
            })
        };
        return new ToolOutput(data, Count(hits.Count, "job"));
    }

    private async Task<Domain.Postings.Posting> FindPostingAsync(IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        // The repository is filtered by user, so another user's posting id reads as not found.
        if (!Guid.TryParse(args.GetValueOrDefault("postingId"), out var id))
        {
            throw new ChatToolException("postingId must be a posting id from list_postings.");
        }

        return await postings.FindAsync(id, ct) ?? throw new ChatToolException("Posting not found.");
    }

    private async Task<Job> FindJobAsync(IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        var id = Arg(args, "jobId");
        var all = await jobs.LoadAsync(ct);
        return all?.FirstOrDefault(j => j.Id == id) ?? throw new ChatToolException("Job not found. Search again.");
    }

    private static ApplicationStage ParseStage(IReadOnlyDictionary<string, string> args) =>
        ApplicationStages.TryParse(args.GetValueOrDefault("stage"), out var stage)
            ? stage
            : throw new ChatToolException("stage must be saved, applied, interview, offer or rejected.");

    private static string Arg(IReadOnlyDictionary<string, string> args, string key) =>
        args.GetValueOrDefault(key) is { Length: > 0 } value ? value : throw new ChatToolException($"{key} is required.");

    private static Dictionary<string, string> Only(IReadOnlyDictionary<string, string> args, params string[] keys) =>
        keys.Where(args.ContainsKey).ToDictionary(k => k, k => args[k].Trim());

    private static string Capitalize(string code) => char.ToUpperInvariant(code[0]) + code[1..];

    internal static string Count(int n, string noun) => $"{n} {noun}{(n == 1 ? "" : "s")}";

    public static string? Clip(string? text, int max = MaxText)
    {
        if (text is null)
        {
            return null;
        }

        var flat = text.ReplaceLineEndings(" ").Trim();
        return flat.Length <= max ? flat : flat[..(max - 1)].TrimEnd() + "…";
    }
}
