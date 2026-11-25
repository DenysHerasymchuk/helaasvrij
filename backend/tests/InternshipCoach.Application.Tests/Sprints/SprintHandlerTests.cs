using InternshipCoach.Application.Sprints;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Sprints;

namespace InternshipCoach.Application.Tests.Sprints;

public class SprintHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 24);

    private readonly FakePostingRepository _postings = new();
    private readonly FakeSprintRepository _sprints = new();
    private readonly FakeApplicationRepository _applications;
    private readonly FakeClock _clock = new(Now);
    private readonly FakeCurrentUser _user = new(UserId);

    public SprintHandlerTests()
    {
        _applications = new FakeApplicationRepository(_postings);
    }

    private Posting AddPosting(string company = "Acme")
    {
        var posting = Posting.Create(UserId, company, "Dev", "Gent", PostingLanguage.En, null, "text", null, [], false, new CreatedAt(Now));
        posting.AssignNumber(_postings.Items.Count + 1);
        _postings.Items.Add(posting);
        return posting;
    }

    private StartSprintHandler Start() => new(_sprints, _user, _clock);

    private AddSprintItemsHandler Add() => new(_sprints, _applications, _postings, _user, _clock);

    private CompleteSprintHandler Complete() => new(_sprints, _applications, _user, _clock);

    private static StartSprintCommand Cmd(string name = "Sprint 1") => new(name, "Apply to five roles", Today, Today.AddDays(14));

    private async Task<Sprint> StartSprint(string name = "Sprint 1") => (await Start().HandleAsync(Cmd(name), default)).Value!;

    [Fact]
    public async Task Second_active_sprint_is_a_conflict()
    {
        await StartSprint();

        var result = await Start().HandleAsync(Cmd("Sprint 2"), default);

        Assert.Equal(SprintOutcome.Conflict, result.Outcome);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_name_is_invalid(string name)
    {
        var result = await Start().HandleAsync(Cmd(name), default);

        Assert.Equal(SprintOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task End_before_start_is_invalid()
    {
        var result = await Start().HandleAsync(new StartSprintCommand("S", "g", Today, Today.AddDays(-1)), default);

        Assert.Equal(SprintOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task Add_items_creates_cards_in_the_sprint_and_skips_ones_already_in_it()
    {
        var sprint = await StartSprint();
        var a = AddPosting("Alpha");
        var b = AddPosting("Beta");

        var first = await Add().HandleAsync(sprint.Id, [a.Id, b.Id], default);
        var again = await Add().HandleAsync(sprint.Id, [a.Id], default);

        Assert.Equal(2, first.Value!.Added);
        Assert.Equal(0, again.Value!.Added);
        Assert.All(_applications.Items, application => Assert.Equal(sprint.Id, application.SprintId));
    }

    [Fact]
    public async Task Add_items_moves_a_backlog_card_into_the_sprint()
    {
        var posting = AddPosting();
        await _applications.AddAsync(JobApplication.Create(UserId, posting.Id, new CreatedAt(Now)), default);
        var sprint = await StartSprint();

        await Add().HandleAsync(sprint.Id, [posting.Id], default);

        Assert.Equal(sprint.Id, Assert.Single(_applications.Items).SprintId);
    }

    [Fact]
    public async Task Add_items_refuses_a_completed_sprint_and_an_unknown_posting()
    {
        var sprint = await StartSprint();
        var posting = AddPosting();

        var unknown = await Add().HandleAsync(sprint.Id, [Guid.NewGuid()], default);
        await Complete().HandleAsync(new CompleteSprintCommand(sprint.Id, null), default);
        var completed = await Add().HandleAsync(sprint.Id, [posting.Id], default);

        Assert.Equal(SprintOutcome.NotFound, unknown.Outcome);
        Assert.Equal(SprintOutcome.Invalid, completed.Outcome);
    }

    [Fact]
    public async Task Remove_is_only_allowed_while_saved()
    {
        var sprint = await StartSprint();
        var saved = AddPosting("Alpha");
        var moved = AddPosting("Beta");
        await Add().HandleAsync(sprint.Id, [saved.Id, moved.Id], default);
        _applications.Items.Single(a => a.PostingId == moved.Id).MoveTo(ApplicationStage.Applied, Now);
        var remove = new RemoveSprintItemHandler(_applications, _clock);

        var ok = await remove.HandleAsync(sprint.Id, saved.Id, default);
        var refused = await remove.HandleAsync(sprint.Id, moved.Id, default);

        Assert.Equal(SprintOutcome.Ok, ok.Outcome);
        Assert.Equal(SprintOutcome.Invalid, refused.Outcome);
        Assert.Null(_applications.Items.Single(a => a.PostingId == saved.Id).SprintId);
    }

    [Fact]
    public async Task Complete_with_next_carries_open_cards_and_leaves_finished_ones()
    {
        var sprint = await StartSprint();
        var open = AddPosting("Alpha");
        var offer = AddPosting("Beta");
        await Add().HandleAsync(sprint.Id, [open.Id, offer.Id], default);
        _applications.Items.Single(a => a.PostingId == open.Id).MoveTo(ApplicationStage.Interview, Now);
        _applications.Items.Single(a => a.PostingId == offer.Id).MoveTo(ApplicationStage.Offer, Now);

        var result = await Complete().HandleAsync(new CompleteSprintCommand(sprint.Id, Cmd("Sprint 2")), default);

        var next = result.Value!.Next!;
        Assert.Equal(1, result.Value.Carried);
        Assert.Equal(next.Id, _applications.Items.Single(a => a.PostingId == open.Id).SprintId);
        Assert.Equal(sprint.Id, _applications.Items.Single(a => a.PostingId == offer.Id).SprintId);
        Assert.Contains(_applications.Items.Single(a => a.PostingId == open.Id).Events, e => e.Type == ApplicationEventType.SprintChanged);
        Assert.Equal(SprintStatus.Completed, sprint.Status);
    }

    [Fact]
    public async Task Complete_without_next_returns_open_cards_to_the_backlog()
    {
        var sprint = await StartSprint();
        var posting = AddPosting();
        await Add().HandleAsync(sprint.Id, [posting.Id], default);

        var result = await Complete().HandleAsync(new CompleteSprintCommand(sprint.Id, null), default);

        Assert.Equal(1, result.Value!.ReturnedToBacklog);
        Assert.Null(Assert.Single(_applications.Items).SprintId);
    }

    [Fact]
    public async Task Backlog_excludes_positions_that_are_in_a_sprint()
    {
        var sprint = await StartSprint();
        var inSprint = AddPosting("Alpha");
        var loose = AddPosting("Beta");
        await Add().HandleAsync(sprint.Id, [inSprint.Id], default);

        var backlog = await new ListBacklogHandler(_postings, _applications).HandleAsync(null, null, default);

        Assert.Equal(loose.Id, Assert.Single(backlog).Posting.Id);
    }

    [Fact]
    public async Task Summary_counts_come_from_the_event_log()
    {
        var sprint = await StartSprint();
        var posting = AddPosting();
        await Add().HandleAsync(sprint.Id, [posting.Id], default);
        _applications.Items.Single().MoveTo(ApplicationStage.Applied, Now);
        _applications.Items.Single().MoveTo(ApplicationStage.Interview, Now);

        var summary = (await new ListSprintsHandler(_sprints, _applications).HandleAsync(default)).Single();

        Assert.Equal(1, summary.Items);
        Assert.Equal(1, summary.Applied);
        Assert.Equal(1, summary.Interviews);
    }
}
