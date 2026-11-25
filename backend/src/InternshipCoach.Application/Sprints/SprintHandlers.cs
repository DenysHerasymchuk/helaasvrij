using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Applications;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Sprints;

namespace InternshipCoach.Application.Sprints;

public enum SprintOutcome
{
    Ok,
    NotFound,
    Invalid,
    Conflict
}

public sealed record SprintResult<T>(SprintOutcome Outcome, T? Value = default, string? Error = null)
{
    public static SprintResult<T> Ok(T value) => new(SprintOutcome.Ok, value);

    public static SprintResult<T> Fail(SprintOutcome outcome, string error) => new(outcome, default, error);
}

/// <param name="Items">Applications now in the sprint.</param>
/// <param name="Open">Items still Saved, Applied or in Interview.</param>
/// <param name="Applied">Applications that reached Applied or beyond while in this sprint.</param>
public sealed record SprintSummary(Sprint Sprint, int Items, int Open, int Applied, int Interviews, int Offers, int Rejected);

public sealed record SprintReportItem(ApplicationCard Card, int? Score);

public sealed record SprintReport(SprintSummary Summary, IReadOnlyList<SprintReportItem> Items, int? AverageScore, int GapsClosed);

public static class SprintSummaries
{
    /// <summary>Counts come from the history log, so a card that moved on still counts for the sprint it moved in.</summary>
    public static SprintSummary Build(Sprint sprint, IReadOnlyList<ApplicationCard> cards, IReadOnlyList<ApplicationEvent> events)
    {
        var inSprint = cards.Where(card => card.Application.SprintId == sprint.Id).ToList();
        var moved = events.Where(e => e.Type == ApplicationEventType.StageChanged && e.SprintId == sprint.Id).ToList();

        int Reached(params ApplicationStage[] stages) =>
            moved.Where(e => e.ToStage is { } stage && stages.Contains(stage)).Select(e => e.ApplicationId).Distinct().Count();

        return new SprintSummary(
            sprint,
            inSprint.Count,
            inSprint.Count(card => card.Application.Stage is ApplicationStage.Saved or ApplicationStage.Applied or ApplicationStage.Interview),
            Reached(ApplicationStage.Applied, ApplicationStage.Interview, ApplicationStage.Offer),
            Reached(ApplicationStage.Interview),
            Reached(ApplicationStage.Offer),
            Reached(ApplicationStage.Rejected));
    }
}

public sealed record StartSprintCommand(string? Name, string? Goal, DateOnly StartsOn, DateOnly EndsOn);

public sealed class StartSprintHandler(ISprintRepository sprints, ICurrentUser currentUser, IClock clock)
{
    public async Task<SprintResult<Sprint>> HandleAsync(StartSprintCommand command, CancellationToken ct)
    {
        if (await sprints.FindActiveAsync(ct) is not null)
        {
            return SprintResult<Sprint>.Fail(SprintOutcome.Conflict, "Complete the running sprint before you start a new one.");
        }

        try
        {
            var sprint = Sprint.Start(currentUser.UserId, command.Name, command.Goal, command.StartsOn, command.EndsOn, new CreatedAt(clock.UtcNow));
            await sprints.AddAsync(sprint, ct);
            return SprintResult<Sprint>.Ok(sprint);
        }
        catch (DomainException ex)
        {
            return SprintResult<Sprint>.Fail(SprintOutcome.Invalid, ex.Message);
        }
    }
}

public sealed class ListSprintsHandler(ISprintRepository sprints, IApplicationRepository applications)
{
    /// <summary>The running sprint first, then finished ones, newest first.</summary>
    public async Task<IReadOnlyList<SprintSummary>> HandleAsync(CancellationToken ct)
    {
        var all = await sprints.ListAsync(ct);
        var cards = await applications.ListAsync(ct);
        var events = await applications.ListEventsAsync(ct);
        return all.OrderBy(sprint => sprint.Status).Select(sprint => SprintSummaries.Build(sprint, cards, events)).ToList();
    }
}

public sealed class GetSprintReportHandler(ISprintRepository sprints, IApplicationRepository applications, IPostingRepository postings)
{
    public async Task<SprintReport?> HandleAsync(Guid id, CancellationToken ct)
    {
        var sprint = await sprints.FindAsync(id, ct);
        if (sprint is null)
        {
            return null;
        }

        var cards = await applications.ListAsync(ct);
        var events = await applications.ListEventsAsync(ct);
        var byPosting = (await postings.ListAsync(null, null, ct)).ToDictionary(p => p.Id);

        var items = cards
            .Where(card => card.Application.SprintId == id && byPosting.ContainsKey(card.Application.PostingId))
            .Select(card =>
            {
                var posting = byPosting[card.Application.PostingId];
                return new SprintReportItem(card, posting.IsAnalysed ? posting.Score() : null);
            })
            .OrderByDescending(item => item.Score ?? -1)
            .ToList();

        var scores = items.Where(item => item.Score is not null).Select(item => item.Score!.Value).ToList();
        var gapsClosed = items.Sum(item => byPosting[item.Card.Application.PostingId].Requirements.Count(r => r.Done));
        return new SprintReport(
            SprintSummaries.Build(sprint, cards, events), items, scores.Count == 0 ? null : (int)Math.Round(scores.Average()), gapsClosed);
    }
}

/// <param name="Added">Positions that moved into the sprint now. Ones already in it, or finished in an earlier sprint, are skipped.</param>
public sealed record AddSprintItemsResult(int Added);

public sealed class AddSprintItemsHandler(
    ISprintRepository sprints, IApplicationRepository applications, IPostingRepository postings, ICurrentUser currentUser, IClock clock)
{
    public async Task<SprintResult<AddSprintItemsResult>> HandleAsync(Guid sprintId, IReadOnlyCollection<Guid> postingIds, CancellationToken ct)
    {
        var sprint = await sprints.FindAsync(sprintId, ct);
        if (sprint is null)
        {
            return SprintResult<AddSprintItemsResult>.Fail(SprintOutcome.NotFound, "Sprint not found.");
        }

        if (sprint.Status != SprintStatus.Active)
        {
            return SprintResult<AddSprintItemsResult>.Fail(SprintOutcome.Invalid, "You can only add positions to the running sprint.");
        }

        var now = clock.UtcNow;
        var added = 0;
        var found = 0;
        foreach (var postingId in postingIds.Distinct())
        {
            // The lookup is filtered by user, so another user's posting id reads as "not found".
            if (await postings.FindAsync(postingId, ct) is null)
            {
                continue;
            }

            found++;
            if (await applications.FindIdForPostingAsync(postingId, ct) is not { } applicationId)
            {
                await applications.AddAsync(JobApplication.Create(currentUser.UserId, postingId, new CreatedAt(now), sprint.Id), ct);
                added++;
                continue;
            }

            var application = await applications.FindAsync(applicationId, ct);
            if (application is { SprintId: null })
            {
                application.AssignToSprint(sprint.Id, now);
                added++;
            }
        }

        if (found == 0)
        {
            return SprintResult<AddSprintItemsResult>.Fail(SprintOutcome.NotFound, "None of those positions exist.");
        }

        await applications.SaveAsync(ct);
        return SprintResult<AddSprintItemsResult>.Ok(new AddSprintItemsResult(added));
    }
}

public sealed class RemoveSprintItemHandler(IApplicationRepository applications, IClock clock)
{
    public async Task<SprintResult<bool>> HandleAsync(Guid sprintId, Guid postingId, CancellationToken ct)
    {
        var applicationId = await applications.FindIdForPostingAsync(postingId, ct);
        var application = applicationId is { } id ? await applications.FindAsync(id, ct) : null;
        if (application is null || application.SprintId != sprintId)
        {
            return SprintResult<bool>.Fail(SprintOutcome.NotFound, "That position is not in this sprint.");
        }

        if (application.Stage != ApplicationStage.Saved)
        {
            return SprintResult<bool>.Fail(SprintOutcome.Invalid, "Only a saved position can go back to the backlog. Move it on, or complete the sprint.");
        }

        application.AssignToSprint(null, clock.UtcNow);
        await applications.SaveAsync(ct);
        return SprintResult<bool>.Ok(true);
    }
}

/// <param name="Next">When set, open positions move into this new sprint. Null sends them back to the backlog.</param>
public sealed record CompleteSprintCommand(Guid Id, StartSprintCommand? Next);

public sealed record CompleteSprintResult(SprintSummary Completed, Sprint? Next, int Carried, int ReturnedToBacklog);

public sealed class CompleteSprintHandler(
    ISprintRepository sprints, IApplicationRepository applications, ICurrentUser currentUser, IClock clock)
{
    private static bool IsOpen(JobApplication application) =>
        application.Stage is ApplicationStage.Saved or ApplicationStage.Applied or ApplicationStage.Interview;

    // shortcut: complete, create the next sprint and move the cards in separate saves, because the one-active-sprint
    // index must never see two active rows; a failure in between leaves open cards in the finished sprint. Upgrade to one transaction.
    public async Task<SprintResult<CompleteSprintResult>> HandleAsync(CompleteSprintCommand command, CancellationToken ct)
    {
        var sprint = await sprints.FindAsync(command.Id, ct);
        if (sprint is null)
        {
            return SprintResult<CompleteSprintResult>.Fail(SprintOutcome.NotFound, "Sprint not found.");
        }

        if (sprint.Status == SprintStatus.Completed)
        {
            return SprintResult<CompleteSprintResult>.Fail(SprintOutcome.Invalid, "This sprint is already completed.");
        }

        var now = clock.UtcNow;
        Sprint? next = null;
        try
        {
            if (command.Next is { } n)
            {
                next = Sprint.Start(currentUser.UserId, n.Name, n.Goal, n.StartsOn, n.EndsOn, new CreatedAt(now));
            }

            sprint.Complete(now);
        }
        catch (DomainException ex)
        {
            return SprintResult<CompleteSprintResult>.Fail(SprintOutcome.Invalid, ex.Message);
        }

        await sprints.SaveAsync(ct);

        var open = (await applications.ListForSprintAsync(sprint.Id, ct)).Where(IsOpen).ToList();
        if (next is not null)
        {
            await sprints.AddAsync(next, ct);
        }

        foreach (var application in open)
        {
            application.AssignToSprint(next?.Id, now);
        }

        await applications.SaveAsync(ct);

        var cards = await applications.ListAsync(ct);
        var events = await applications.ListEventsAsync(ct);
        return SprintResult<CompleteSprintResult>.Ok(new CompleteSprintResult(
            SprintSummaries.Build(sprint, cards, events), next, next is null ? 0 : open.Count, next is null ? open.Count : 0));
    }
}

public sealed record BacklogItem(Posting Posting, ApplicationStage? Stage);

public sealed class ListBacklogHandler(IPostingRepository postings, IApplicationRepository applications)
{
    /// <summary>Positions that are not in any sprint, newest first. A position that has a card but no sprint keeps its stage.</summary>
    public async Task<IReadOnlyList<BacklogItem>> HandleAsync(PostingLanguage? language, string? query, CancellationToken ct)
    {
        var all = await postings.ListAsync(language, query, ct);
        var cards = (await applications.ListAsync(ct)).ToDictionary(card => card.Application.PostingId);
        return all
            .Where(posting => !cards.TryGetValue(posting.Id, out var card) || card.Application.SprintId is null)
            .Select(posting => new BacklogItem(posting, cards.TryGetValue(posting.Id, out var card) ? card.Application.Stage : null))
            .ToList();
    }
}

public sealed record ApplicationEventView(ApplicationEvent Event, string? SprintName);

public sealed class ListApplicationEventsHandler(IApplicationRepository applications, ISprintRepository sprints)
{
    public async Task<IReadOnlyList<ApplicationEventView>> HandleAsync(Guid applicationId, CancellationToken ct)
    {
        var events = await applications.ListEventsAsync(applicationId, ct);
        if (events.Count == 0)
        {
            return [];
        }

        var names = (await sprints.ListAsync(ct)).ToDictionary(sprint => sprint.Id, sprint => sprint.Name);
        return events.Select(e => new ApplicationEventView(e, e.SprintId is { } id && names.TryGetValue(id, out var name) ? name : null)).ToList();
    }
}

/// <param name="Scope">Positions in the sprint that day (joined, not yet taken out).</param>
/// <param name="Open">Of those, the ones still saved, applied or in interview.</param>
public sealed record BurndownPoint(DateOnly Date, int Scope, int Open);

public sealed class GetSprintBurndownHandler(ISprintRepository sprints, IApplicationRepository applications, IClock clock)
{
    // shortcut: days are UTC dates, not Brussels dates; a move near midnight can land on the next day. Upgrade when the chart needs exact days.
    /// <summary>One point per day from the sprint start to its end (or today, or the completion day). Null when the sprint does not exist.</summary>
    public async Task<IReadOnlyList<BurndownPoint>?> HandleAsync(Guid sprintId, CancellationToken ct)
    {
        var sprint = await sprints.FindAsync(sprintId, ct);
        if (sprint is null)
        {
            return null;
        }

        var events = (await applications.ListEventsAsync(ct)).Where(e => e.SprintId == sprintId || e.Type == ApplicationEventType.SprintChanged).ToList();
        var byApplication = events.GroupBy(e => e.ApplicationId).ToList();

        // Per application: the day it joined, the day it reached Offer or Rejected here, the day it left.
        var spans = new List<(DateOnly Joined, DateOnly? Closed, DateOnly? Left)>();
        foreach (var group in byApplication)
        {
            var ordered = group.OrderBy(e => e.At).ToList();
            var join = ordered.FirstOrDefault(e => e.SprintId == sprintId && e.Type is ApplicationEventType.Added or ApplicationEventType.SprintChanged);
            if (join is null)
            {
                continue;
            }

            var closed = ordered.FirstOrDefault(e => e.Type == ApplicationEventType.StageChanged && e.SprintId == sprintId && e.ToStage is ApplicationStage.Offer or ApplicationStage.Rejected);
            var left = ordered.FirstOrDefault(e => e.Type == ApplicationEventType.SprintChanged && e.SprintId != sprintId && e.At > join.At);
            spans.Add((DateOnly.FromDateTime(join.At.UtcDateTime), closed is null ? null : DateOnly.FromDateTime(closed.At.UtcDateTime), left is null ? null : DateOnly.FromDateTime(left.At.UtcDateTime)));
        }

        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var last = sprint.CompletedAt is { } done ? DateOnly.FromDateTime(done.UtcDateTime) : today < sprint.EndsOn ? today : sprint.EndsOn;
        var points = new List<BurndownPoint>();
        for (var day = sprint.StartsOn; day <= last; day = day.AddDays(1))
        {
            var inScope = spans.Where(s => s.Joined <= day && (s.Left is null || s.Left > day)).ToList();
            points.Add(new BurndownPoint(day, inScope.Count, inScope.Count(s => s.Closed is null || s.Closed > day)));
        }

        return points;
    }
}

public sealed record ActivityItem(ApplicationEvent Event, Guid PostingId, string Key, string Title, string Company, string? SprintName);

public sealed class ListRecentActivityHandler(IApplicationRepository applications, ISprintRepository sprints)
{
    public const int MaxTake = 50;

    /// <summary>The latest history events across all of the user's positions, newest first.</summary>
    public async Task<IReadOnlyList<ActivityItem>> HandleAsync(int take, CancellationToken ct)
    {
        var cards = (await applications.ListAsync(ct)).ToDictionary(card => card.Application.Id);
        var names = (await sprints.ListAsync(ct)).ToDictionary(sprint => sprint.Id, sprint => sprint.Name);
        return (await applications.ListEventsAsync(ct))
            .Where(e => cards.ContainsKey(e.ApplicationId))
            .OrderByDescending(e => e.At)
            .Take(Math.Clamp(take, 1, MaxTake))
            .Select(e =>
            {
                var card = cards[e.ApplicationId];
                return new ActivityItem(e, card.Application.PostingId, card.Key, card.Title, card.Company, e.SprintId is { } id && names.TryGetValue(id, out var name) ? name : null);
            })
            .ToList();
    }
}
