using InternshipCoach.Application.Applications;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Tests.Applications;

public class ApplicationHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    private readonly FakePostingRepository _postings = new();
    private readonly FakeSprintRepository _sprints = new();
    private readonly FakeApplicationRepository _applications;
    private readonly FakeClock _clock = new(Now);

    public ApplicationHandlerTests()
    {
        _applications = new FakeApplicationRepository(_postings);
    }

    private Posting AddPosting(Guid? owner = null)
    {
        var posting = Posting.Create(owner ?? UserId, "Acme", "Intern", "Gent", PostingLanguage.En, null, "text", null, [], false, new CreatedAt(Now));
        _postings.Items.Add(posting);
        return posting;
    }

    private CreateApplicationHandler Create() => new(_applications, _postings, _sprints, new FakeCurrentUser(UserId), _clock);

    private UpdateApplicationHandler Update() => new(_applications, _clock);

    [Fact]
    public async Task Create_starts_in_saved_with_posting_details()
    {
        var posting = AddPosting();

        var result = await Create().HandleAsync(new CreateApplicationCommand(posting.Id), default);

        Assert.Equal(ApplicationOutcome.Ok, result.Outcome);
        Assert.Equal("saved", result.Card!.Application.Stage.ToCode());
        Assert.Equal("Acme", result.Card.Company);
        Assert.Null(result.Card.Application.AppliedAt);
    }

    [Fact]
    public async Task Create_for_unknown_posting_is_not_found()
    {
        var result = await Create().HandleAsync(new CreateApplicationCommand(Guid.NewGuid()), default);

        Assert.Equal(ApplicationOutcome.NotFound, result.Outcome);
        Assert.Empty(_applications.Items);
    }

    [Fact]
    public async Task Create_twice_for_one_posting_returns_the_existing_application()
    {
        var posting = AddPosting();
        var first = await Create().HandleAsync(new CreateApplicationCommand(posting.Id), default);
        await Update().HandleAsync(new UpdateApplicationCommand(first.Card!.Application.Id, "applied", null), default);

        var result = await Create().HandleAsync(new CreateApplicationCommand(posting.Id), default);

        Assert.Equal(ApplicationOutcome.Ok, result.Outcome);
        Assert.Equal(first.Card.Application.Id, result.Card!.Application.Id);
        Assert.Equal("applied", result.Card.Application.Stage.ToCode());
        Assert.Single(_applications.Items);
    }

    [Fact]
    public async Task Moving_to_applied_sets_applied_at_once_and_keeps_it_later()
    {
        var posting = AddPosting();
        var id = (await Create().HandleAsync(new CreateApplicationCommand(posting.Id), default)).Card!.Application.Id;

        await Update().HandleAsync(new UpdateApplicationCommand(id, "applied", null), default);
        var appliedAt = _applications.Items[0].AppliedAt;
        _clock.Set(Now.AddDays(3));
        await Update().HandleAsync(new UpdateApplicationCommand(id, "interview", null), default);

        Assert.Equal(Now, appliedAt);
        Assert.Equal(Now, _applications.Items[0].AppliedAt);
        Assert.Equal(Now.AddDays(3), _applications.Items[0].StageChangedAt);
    }

    [Fact]
    public async Task Update_changes_notes_without_touching_the_stage()
    {
        var posting = AddPosting();
        var id = (await Create().HandleAsync(new CreateApplicationCommand(posting.Id), default)).Card!.Application.Id;

        var result = await Update().HandleAsync(new UpdateApplicationCommand(id, null, "  Call on Monday "), default);

        Assert.Equal(ApplicationOutcome.Ok, result.Outcome);
        Assert.Equal("Call on Monday", result.Card!.Application.Notes);
        Assert.Equal(ApplicationStage.Saved, result.Card.Application.Stage);
    }

    [Theory]
    [InlineData("done")]
    [InlineData("")]
    public async Task Update_rejects_an_unknown_stage(string stage)
    {
        var posting = AddPosting();
        var id = (await Create().HandleAsync(new CreateApplicationCommand(posting.Id), default)).Card!.Application.Id;

        var result = await Update().HandleAsync(new UpdateApplicationCommand(id, stage, null), default);

        Assert.Equal(ApplicationOutcome.Invalid, result.Outcome);
        Assert.Equal(ApplicationStage.Saved, _applications.Items[0].Stage);
    }

    [Fact]
    public async Task Update_rejects_notes_over_the_limit()
    {
        var posting = AddPosting();
        var id = (await Create().HandleAsync(new CreateApplicationCommand(posting.Id), default)).Card!.Application.Id;

        var result = await Update().HandleAsync(new UpdateApplicationCommand(id, null, new string('x', JobApplication.NotesMaxLength + 1)), default);

        Assert.Equal(ApplicationOutcome.Invalid, result.Outcome);
    }

    [Fact]
    public async Task Update_of_a_missing_application_is_not_found()
    {
        var result = await Update().HandleAsync(new UpdateApplicationCommand(Guid.NewGuid(), "applied", null), default);

        Assert.Equal(ApplicationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Stats_cover_the_requested_months_oldest_first_with_empty_months()
    {
        var applied = AddPosting();
        var interview = AddPosting();
        var offer = AddPosting();
        foreach (var (posting, stage, at) in new[]
                 {
                     (applied, "applied", Now.AddMonths(-2)),
                     (interview, "interview", Now.AddMonths(-1)),
                     (offer, "offer", Now)
                 })
        {
            _clock.Set(at);
            var id = (await Create().HandleAsync(new CreateApplicationCommand(posting.Id), default)).Card!.Application.Id;
            await Update().HandleAsync(new UpdateApplicationCommand(id, stage, null), default);
        }

        _clock.Set(Now);
        var stats = await new GetApplicationStatsHandler(_applications, _clock).HandleAsync(4, default);

        Assert.Equal(["Aug", "Sep", "Oct", "Nov"], stats.Select(m => m.Month));
        Assert.Equal([0, 1, 1, 1], stats.Select(m => m.Applied));
        Assert.Equal([0, 0, 1, 0], stats.Select(m => m.Interview));
        Assert.Equal([0, 0, 0, 1], stats.Select(m => m.Offer));
    }

    [Fact]
    public async Task Stats_default_to_six_months_and_clamp_the_range()
    {
        var handler = new GetApplicationStatsHandler(_applications, _clock);

        Assert.Equal(6, (await handler.HandleAsync(null, default)).Count);
        Assert.Single(await handler.HandleAsync(0, default));
        Assert.Equal(GetApplicationStatsHandler.MaxMonths, (await handler.HandleAsync(999, default)).Count);
    }
}
