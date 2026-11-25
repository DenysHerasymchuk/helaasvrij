using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Applications;
using InternshipCoach.Application.Calendar;
using InternshipCoach.Application.Documents.ListDocuments;
using InternshipCoach.Application.Gaps;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Postings.Add;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Application.Sources;
using InternshipCoach.Application.Sprints;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Chat.Tools;

/// <summary>
/// The sprint, backlog, activity, calendar, document, notes and gap tools. Same rules as <see cref="ChatToolbox"/>:
/// every lookup goes through user-scoped repositories and handlers, results are trimmed, actions only run after a confirm.
/// </summary>
public sealed class PlanningTools(
    ISprintRepository sprints,
    IApplicationRepository applications,
    IPostingRepository postings,
    ListSprintsHandler listSprints,
    GetSprintReportHandler sprintReport,
    ListBacklogHandler backlog,
    ListRecentActivityHandler activity,
    ListApplicationEventsHandler events,
    GetCalendarHandler calendar,
    ListDocumentsHandler documents,
    AddSprintItemsHandler addItems,
    RemoveSprintItemHandler removeItem,
    StartSprintHandler startSprint,
    CompleteSprintHandler completeSprint,
    UpdateApplicationHandler updateApplication,
    MarkGapDoneHandler markGap,
    UndoGapHandler undoGap,
    AddPostingFromPasteHandler addByText,
    AddPostingFromUrlHandler addByUrl,
    IClock clock)
{
    private const int DefaultSprintDays = 14;
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    private static readonly ToolParam PostingId = new("postingId", "id from list_postings or list_backlog");

    public static readonly IReadOnlyList<ToolSpec> ReadSpecs =
    [
        ToolSpec.Read("get_active_sprint", "the running sprint: name, goal, days left and its positions by stage"),
        ToolSpec.Read("list_sprints", "every sprint with counts (positions, open, applied, interviews, offers, rejected)"),
        ToolSpec.Read("get_sprint_report", "report of one sprint: positions, average match, gaps closed", new ToolParam("sprintId", "id from list_sprints")),
        ToolSpec.Read("list_backlog", "positions that are in no sprint yet"),
        ToolSpec.Read("get_activity", "latest stage moves and sprint changes across all positions", new ToolParam("take", "how many, 1 to 20", false)),
        ToolSpec.Read("get_calendar", "interviews, deadlines and gap due dates in the next 14 days"),
        ToolSpec.Read("list_documents", "saved CVs, cover letters and recruiter messages (title, kind, language, updated)"),
        ToolSpec.Read("get_position_activity", "the history of one position", PostingId)
    ];

    public static readonly IReadOnlyList<ToolSpec> ActionSpecs =
    [
        ToolSpec.Action("add_to_sprint", "put a position in the running sprint", PostingId),
        ToolSpec.Action("remove_from_sprint", "send a Saved position of the running sprint back to the backlog", PostingId),
        ToolSpec.Action("start_sprint", "start a sprint (only when none is running)",
            new ToolParam("name", "sprint name"), new ToolParam("goal", "one sentence goal", false), new ToolParam("days", "length in days, default 14", false)),
        ToolSpec.Action("complete_sprint", "complete the running sprint; open positions return to the backlog"),
        ToolSpec.Action("add_position", "add a position from its text or from a link (not LinkedIn); needs text or url",
            new ToolParam("text", "the full posting text", false), new ToolParam("url", "link of the posting", false)),
        ToolSpec.Action("set_notes", "replace the notes on a position's card", PostingId, new ToolParam("notes", "the new notes, at most 2000 characters")),
        ToolSpec.Action("tick_gap", "tick a gap as done: re-reads that repo and re-checks the requirement (uses AI)", new ToolParam("requirementId", "id of the requirement")),
        ToolSpec.Action("untick_gap", "undo a ticked gap", new ToolParam("requirementId", "id of the requirement"))
    ];

    public static bool Handles(string name) => ReadSpecs.Concat(ActionSpecs).Any(t => t.Name == name);

    private DateOnly Today() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, Brussels).DateTime);

    public async Task<ToolOutput> RunReadAsync(string name, IReadOnlyDictionary<string, string> args, CancellationToken ct) => name switch
    {
        "get_active_sprint" => await GetActiveSprintAsync(ct),
        "list_sprints" => await ListSprintsAsync(ct),
        "get_sprint_report" => await GetSprintReportAsync(args, ct),
        "list_backlog" => await ListBacklogAsync(ct),
        "get_activity" => await GetActivityAsync(args, ct),
        "get_calendar" => await GetCalendarAsync(ct),
        "list_documents" => await ListDocumentsAsync(ct),
        "get_position_activity" => await GetPositionActivityAsync(args, ct),
        _ => throw new ChatToolException($"Unknown tool {ChatToolbox.Clip(name, 40)}.")
    };

    /// <summary>Checks an action and words the question. Runs nothing.</summary>
    public async Task<PendingChatAction> ProposeAsync(string tool, IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        switch (tool)
        {
            case "add_to_sprint":
            {
                var posting = await FindPostingAsync(args, ct);
                var sprint = await sprints.FindActiveAsync(ct) ?? throw new ChatToolException("There is no running sprint. Start one first.");
                return new PendingChatAction(tool, Only(args, "postingId"), $"Add {posting.Key} ({ChatToolbox.Clip(posting.Title, 60)}) to {sprint.Name}?");
            }
            case "remove_from_sprint":
            {
                var posting = await FindPostingAsync(args, ct);
                return new PendingChatAction(tool, Only(args, "postingId"), $"Send {posting.Key} back to the backlog?");
            }
            case "start_sprint":
            {
                var name = Arg(args, "name");
                var days = Days(args);
                if (await sprints.FindActiveAsync(ct) is not null)
                {
                    throw new ChatToolException("A sprint is already running. Complete it first.");
                }

                return new PendingChatAction(tool, Only(args, "name", "goal", "days"), $"Start sprint \"{ChatToolbox.Clip(name, 60)}\" for {days} days?");
            }
            case "complete_sprint":
            {
                var sprint = await sprints.FindActiveAsync(ct) ?? throw new ChatToolException("There is no running sprint.");
                return new PendingChatAction(tool, new Dictionary<string, string>(), $"Complete {sprint.Name}? Open positions go back to the backlog.");
            }
            case "add_position":
            {
                if (string.IsNullOrWhiteSpace(args.GetValueOrDefault("text")) && string.IsNullOrWhiteSpace(args.GetValueOrDefault("url")))
                {
                    throw new ChatToolException("text or url is required.");
                }

                return new PendingChatAction(tool, Only(args, "text", "url"),
                    args.GetValueOrDefault("url") is { Length: > 0 } url ? $"Add the position at {ChatToolbox.Clip(url, 80)}?" : "Add this position from the text?");
            }
            case "set_notes":
            {
                var posting = await FindPostingAsync(args, ct);
                _ = Arg(args, "notes");
                return new PendingChatAction(tool, Only(args, "postingId", "notes"), $"Replace the notes on {posting.Key}?");
            }
            case "tick_gap":
            case "untick_gap":
            {
                var (posting, requirement) = await FindRequirementAsync(args, ct);
                var verb = tool == "tick_gap" ? "Tick" : "Undo";
                return new PendingChatAction(tool, Only(args, "requirementId"), $"{verb} \"{ChatToolbox.Clip(requirement.Text, 80)}\" on {posting.Key}?");
            }
            default:
                throw new ChatToolException($"{ChatToolbox.Clip(tool, 40)} is not an action the assistant may propose.");
        }
    }

    /// <summary>Runs a confirmed action after checking ownership and arguments again.</summary>
    public async Task<ChatActionResult> ExecuteAsync(string tool, IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        switch (tool)
        {
            case "add_to_sprint":
            {
                var posting = await FindPostingAsync(args, ct);
                var sprint = await sprints.FindActiveAsync(ct) ?? throw new ChatToolException("There is no running sprint.");
                var result = await addItems.HandleAsync(sprint.Id, [posting.Id], ct);
                return FromSprint(result.Outcome, result.Error, $"Added {posting.Key} to {sprint.Name}.", "/board");
            }
            case "remove_from_sprint":
            {
                var posting = await FindPostingAsync(args, ct);
                var sprint = await sprints.FindActiveAsync(ct) ?? throw new ChatToolException("There is no running sprint.");
                var result = await removeItem.HandleAsync(sprint.Id, posting.Id, ct);
                return FromSprint(result.Outcome, result.Error, $"Sent {posting.Key} back to the backlog.", "/backlog");
            }
            case "start_sprint":
            {
                var today = Today();
                var result = await startSprint.HandleAsync(
                    new StartSprintCommand(Arg(args, "name"), args.GetValueOrDefault("goal"), today, today.AddDays(Days(args))), ct);
                return FromSprint(result.Outcome, result.Error, $"Started {result.Value?.Name}.", "/board");
            }
            case "complete_sprint":
            {
                var sprint = await sprints.FindActiveAsync(ct) ?? throw new ChatToolException("There is no running sprint.");
                var result = await completeSprint.HandleAsync(new CompleteSprintCommand(sprint.Id, null), ct);
                return FromSprint(result.Outcome, result.Error,
                    $"Completed {sprint.Name}. {result.Value?.ReturnedToBacklog ?? 0} open positions went back to the backlog.", $"/sprints/{sprint.Id}");
            }
            case "add_position":
            {
                var url = args.GetValueOrDefault("url");
                var text = args.GetValueOrDefault("text");
                var outcome = string.IsNullOrWhiteSpace(text)
                    ? await addByUrl.HandleAsync(new AddPostingFromUrlCommand(url), ct)
                    : await addByText.HandleAsync(new AddPostingFromPasteCommand(text, url), ct);
                return outcome.Posting is { } saved
                    ? new ChatActionResult(ChatActionStatus.Ok,
                        outcome.Status == PostingStatus.Existing ? $"{saved.Key} was already saved." : $"Added {saved.Key}: {ChatToolbox.Clip(saved.Title, 60)} at {saved.Company}.",
                        $"/positions/{saved.Id}")
                    : new ChatActionResult(
                        outcome.Status == PostingStatus.NotFound ? ChatActionStatus.NotFound : ChatActionStatus.Invalid,
                        outcome.Message ?? "Could not add this position.");
            }
            case "set_notes":
            {
                var posting = await FindPostingAsync(args, ct);
                var id = await applications.FindIdForPostingAsync(posting.Id, ct)
                         ?? throw new ChatToolException("Put the position on the board first (add it to the sprint).");
                var result = await updateApplication.HandleAsync(new UpdateApplicationCommand(id, null, Arg(args, "notes")), ct);
                return result.Outcome == ApplicationOutcome.Ok
                    ? new ChatActionResult(ChatActionStatus.Ok, $"Updated the notes on {posting.Key}.", $"/positions/{posting.Id}")
                    : new ChatActionResult(result.Outcome == ApplicationOutcome.NotFound ? ChatActionStatus.NotFound : ChatActionStatus.Invalid,
                        result.Error ?? "Could not save the notes.");
            }
            case "tick_gap":
            {
                var (posting, requirement) = await FindRequirementAsync(args, ct);
                try
                {
                    var rescan = await markGap.HandleAsync(requirement.Id, ct);
                    return rescan is null
                        ? new ChatActionResult(ChatActionStatus.NotFound, "Gap not found.")
                        : new ChatActionResult(ChatActionStatus.Ok, $"Ticked it. {posting.Key} went from {rescan.OldScore}% to {rescan.NewScore}%.", "/gaps");
                }
                catch (Exception ex) when (ex is GapInputException or RescanThrottledException or SourceFetchException or LlmException)
                {
                    // The same cases GapsController maps to 4xx/5xx; the message is written for the student.
                    return new ChatActionResult(ChatActionStatus.Invalid, ex.Message);
                }
            }
            case "untick_gap":
            {
                var (_, requirement) = await FindRequirementAsync(args, ct);
                return await undoGap.HandleAsync(requirement.Id, ct) is null
                    ? new ChatActionResult(ChatActionStatus.NotFound, "Gap not found.")
                    : new ChatActionResult(ChatActionStatus.Ok, "Undone.", "/gaps");
            }
            default:
                return new ChatActionResult(ChatActionStatus.Invalid, "This action is not allowed.");
        }
    }

    private static ChatActionResult FromSprint(SprintOutcome outcome, string? error, string ok, string navigateTo) => outcome switch
    {
        SprintOutcome.Ok => new ChatActionResult(ChatActionStatus.Ok, ok, navigateTo),
        SprintOutcome.NotFound => new ChatActionResult(ChatActionStatus.NotFound, error ?? "Not found."),
        _ => new ChatActionResult(ChatActionStatus.Invalid, error ?? "That did not work.")
    };

    private async Task<ToolOutput> GetActiveSprintAsync(CancellationToken ct)
    {
        var sprint = await sprints.FindActiveAsync(ct);
        if (sprint is null)
        {
            return new ToolOutput(new { active = false }, "no running sprint");
        }

        var cards = (await applications.ListAsync(ct)).Where(c => c.Application.SprintId == sprint.Id).ToList();
        var data = new
        {
            active = true,
            id = sprint.Id,
            name = ChatToolbox.Clip(sprint.Name, 80),
            goal = ChatToolbox.Clip(sprint.Goal),
            startsOn = sprint.StartsOn,
            endsOn = sprint.EndsOn,
            daysLeft = Math.Max(0, sprint.EndsOn.DayNumber - Today().DayNumber),
            positions = cards.Take(30).Select(c => new
            {
                key = c.Key, postingId = c.Application.PostingId, company = ChatToolbox.Clip(c.Company, 100), title = ChatToolbox.Clip(c.Title, 100),
                stage = c.Application.Stage.ToCode(), deadline = c.Deadline
            })
        };
        return new ToolOutput(data, $"{ChatToolbox.Clip(sprint.Name, 40)} · {ChatToolbox.Count(cards.Count, "position")}");
    }

    private async Task<ToolOutput> ListSprintsAsync(CancellationToken ct)
    {
        var all = await listSprints.HandleAsync(ct);
        var data = all.Take(20).Select(s => new
        {
            id = s.Sprint.Id, name = ChatToolbox.Clip(s.Sprint.Name, 80), status = s.Sprint.Status.ToString().ToLowerInvariant(),
            startsOn = s.Sprint.StartsOn, endsOn = s.Sprint.EndsOn, positions = s.Items, s.Open, s.Applied, s.Interviews, s.Offers, s.Rejected
        });
        return new ToolOutput(new { sprints = data }, ChatToolbox.Count(all.Count, "sprint"));
    }

    private async Task<ToolOutput> GetSprintReportAsync(IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        if (!Guid.TryParse(args.GetValueOrDefault("sprintId"), out var id))
        {
            throw new ChatToolException("sprintId must be an id from list_sprints.");
        }

        var report = await sprintReport.HandleAsync(id, ct) ?? throw new ChatToolException("Sprint not found.");
        var s = report.Summary;
        var data = new
        {
            name = ChatToolbox.Clip(s.Sprint.Name, 80), goal = ChatToolbox.Clip(s.Sprint.Goal), status = s.Sprint.Status.ToString().ToLowerInvariant(),
            positions = s.Items, s.Applied, s.Interviews, s.Offers, s.Rejected, averageMatch = report.AverageScore, gapsClosed = report.GapsClosed,
            items = report.Items.Take(30).Select(i => new
            {
                key = i.Card.Key, company = ChatToolbox.Clip(i.Card.Company, 100), title = ChatToolbox.Clip(i.Card.Title, 100),
                stage = i.Card.Application.Stage.ToCode(), score = i.Score
            })
        };
        return new ToolOutput(data, ChatToolbox.Clip(s.Sprint.Name, 40)!);
    }

    private async Task<ToolOutput> ListBacklogAsync(CancellationToken ct)
    {
        var items = await backlog.HandleAsync(null, null, ct);
        var data = items.Take(30).Select(i => new
        {
            key = i.Posting.Key, postingId = i.Posting.Id, company = ChatToolbox.Clip(i.Posting.Company, 100), title = ChatToolbox.Clip(i.Posting.Title, 100),
            score = i.Posting.IsAnalysed ? i.Posting.Score() : (int?)null, stage = i.Stage?.ToCode(), deadline = i.Posting.Deadline
        });
        return new ToolOutput(new { positions = data }, ChatToolbox.Count(items.Count, "position"));
    }

    private async Task<ToolOutput> GetActivityAsync(IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        var take = int.TryParse(args.GetValueOrDefault("take"), out var n) ? Math.Clamp(n, 1, 20) : 10;
        var items = await activity.HandleAsync(take, ct);
        var data = items.Select(a => new
        {
            type = a.Event.Type.ToString(), from = a.Event.FromStage?.ToCode(), to = a.Event.ToStage?.ToCode(), at = a.Event.At,
            key = a.Key, postingId = a.PostingId, company = ChatToolbox.Clip(a.Company, 100), sprint = a.SprintName
        });
        return new ToolOutput(new { activity = data }, ChatToolbox.Count(items.Count, "event"));
    }

    private async Task<ToolOutput> GetCalendarAsync(CancellationToken ct)
    {
        var today = Today();
        var items = await calendar.HandleAsync(today, today.AddDays(14), ct);
        var data = items.Take(30).Select(e => new
        {
            type = e.Type.ToString(), date = e.Date, at = e.At, title = ChatToolbox.Clip(e.Title, 120), postingId = e.PostingId
        });
        return new ToolOutput(new { today, events = data }, ChatToolbox.Count(items.Count, "date"));
    }

    private async Task<ToolOutput> ListDocumentsAsync(CancellationToken ct)
    {
        var items = await documents.HandleAsync(ct);
        var data = items.Take(30).Select(d => new
        {
            id = d.Id, title = ChatToolbox.Clip(d.Title, 100), kind = d.Kind.ToString().ToLowerInvariant(), language = d.Language, updatedAt = d.UpdatedAt
        });
        return new ToolOutput(new { documents = data }, ChatToolbox.Count(items.Count, "document"));
    }

    private async Task<ToolOutput> GetPositionActivityAsync(IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        var posting = await FindPostingAsync(args, ct);
        var applicationId = await applications.FindIdForPostingAsync(posting.Id, ct);
        var list = applicationId is { } id ? await events.HandleAsync(id, ct) : [];
        var data = list.Select(v => new
        {
            type = v.Event.Type.ToString(), from = v.Event.FromStage?.ToCode(), to = v.Event.ToStage?.ToCode(), at = v.Event.At, sprint = v.SprintName
        });
        return new ToolOutput(new { key = posting.Key, events = data }, ChatToolbox.Count(list.Count, "event"));
    }

    private async Task<Posting> FindPostingAsync(IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        // The repository is filtered by user, so another user's id reads as not found.
        if (!Guid.TryParse(args.GetValueOrDefault("postingId"), out var id))
        {
            throw new ChatToolException("postingId must be a position id from list_postings or list_backlog.");
        }

        return await postings.FindAsync(id, ct) ?? throw new ChatToolException("Posting not found.");
    }

    private async Task<(Posting Posting, Requirement Requirement)> FindRequirementAsync(IReadOnlyDictionary<string, string> args, CancellationToken ct)
    {
        if (!Guid.TryParse(args.GetValueOrDefault("requirementId"), out var id))
        {
            throw new ChatToolException("requirementId must be an id from list_gaps.");
        }

        foreach (var posting in await postings.ListAsync(null, null, ct))
        {
            if (posting.Requirements.FirstOrDefault(r => r.Id == id) is { } requirement)
            {
                return (posting, requirement);
            }
        }

        throw new ChatToolException("Gap not found.");
    }

    private static int Days(IReadOnlyDictionary<string, string> args) =>
        int.TryParse(args.GetValueOrDefault("days"), out var d) ? Math.Clamp(d, 1, 90) : DefaultSprintDays;

    private static string Arg(IReadOnlyDictionary<string, string> args, string key) =>
        args.GetValueOrDefault(key) is { Length: > 0 } value ? value : throw new ChatToolException($"{key} is required.");

    private static Dictionary<string, string> Only(IReadOnlyDictionary<string, string> args, params string[] keys) =>
        keys.Where(args.ContainsKey).ToDictionary(k => k, k => args[k].Trim());
}
