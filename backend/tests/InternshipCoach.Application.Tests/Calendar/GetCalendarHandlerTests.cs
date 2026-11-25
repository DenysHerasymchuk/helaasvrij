using InternshipCoach.Application.Calendar;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Tests.Calendar;

public class GetCalendarHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly From = new(2026, 12, 1);
    private static readonly DateOnly To = new(2026, 12, 31);

    private readonly FakePostingRepository _postings = new();
    private readonly FakeApplicationRepository _applications;

    public GetCalendarHandlerTests()
    {
        _applications = new FakeApplicationRepository(_postings);
    }

    private GetCalendarHandler Handler() => new(_postings, _applications, new FakeCurrentUser(UserId));

    private Posting AddPosting(string company, DateOnly? deadline = null, Guid? owner = null)
    {
        var posting = Posting.Create(owner ?? UserId, company, "Intern", "Gent", PostingLanguage.En, null, "text", deadline,
            [("Docker experience", Importance.Required), ("Testing", Importance.Nice)], false, new CreatedAt(Now));
        _postings.Items.Add(posting);
        return posting;
    }

    private void MoveTo(Posting posting, ApplicationStage stage)
    {
        var application = JobApplication.Create(posting.UserId, posting.Id, new CreatedAt(Now));
        application.MoveTo(stage, Now);
        _applications.Items.Add(application);
    }

    [Fact]
    public async Task Returns_every_kind_of_event_sorted_by_date()
    {
        var flux = AddPosting("Flux", deadline: new DateOnly(2026, 12, 10));
        flux.SetDates(flux.Deadline, new DateTimeOffset(2026, 12, 3, 13, 0, 0, TimeSpan.Zero));
        flux.Requirements[0].RecordAnalysis(Coverage.None, null, null, "Add a Dockerfile to weather-app", "weather-app", new DateOnly(2026, 12, 5), Now);
        flux.Requirements[1].RecordAnalysis(Coverage.None, null, null, "Add tests to weather-app", "weather-app", new DateOnly(2026, 12, 6), Now);
        flux.Requirements[1].MarkDone();

        var events = await Handler().HandleAsync(From, To, default);

        Assert.Equal(
            [CalendarEventType.Interview, CalendarEventType.GapDue, CalendarEventType.Deadline],
            events.Select(e => e.Type));
        var interview = events[0];
        Assert.Equal("Interview at Flux", interview.Title);
        Assert.Equal(new DateOnly(2026, 12, 3), interview.Date);
        Assert.Equal(flux.Id, interview.PostingId);
        Assert.Equal($"/postings/{flux.Id}", interview.Link);
        Assert.Equal($"/gaps?postingId={flux.Id}", events[1].Link);
    }

    [Fact]
    public async Task Interview_date_is_the_belgian_day()
    {
        var flux = AddPosting("Flux");
        // 23:30 UTC on 30 November is 00:30 on 1 December in Brussels.
        flux.SetDates(null, new DateTimeOffset(2026, 11, 30, 23, 30, 0, TimeSpan.Zero));

        var events = await Handler().HandleAsync(From, To, default);

        Assert.Equal(new DateOnly(2026, 12, 1), Assert.Single(events).Date);
    }

    [Fact]
    public async Task Leaves_out_events_outside_the_range()
    {
        AddPosting("Early", deadline: new DateOnly(2026, 11, 30));
        AddPosting("Late", deadline: new DateOnly(2027, 1, 1));
        AddPosting("Edge", deadline: To);

        var events = await Handler().HandleAsync(From, To, default);

        Assert.Equal("Application deadline: Edge", Assert.Single(events).Title);
    }

    [Fact]
    public async Task Leaves_out_deadlines_of_rejected_applications_but_keeps_their_interviews()
    {
        var rejected = AddPosting("Rejected", deadline: new DateOnly(2026, 12, 10));
        rejected.SetDates(rejected.Deadline, new DateTimeOffset(2026, 12, 4, 9, 0, 0, TimeSpan.Zero));
        MoveTo(rejected, ApplicationStage.Rejected);
        var applied = AddPosting("Applied", deadline: new DateOnly(2026, 12, 11));
        MoveTo(applied, ApplicationStage.Applied);

        var events = await Handler().HandleAsync(From, To, default);

        Assert.DoesNotContain(events, e => e.Type == CalendarEventType.Deadline && e.PostingId == rejected.Id);
        Assert.Contains(events, e => e.Type == CalendarEventType.Interview && e.PostingId == rejected.Id);
        Assert.Contains(events, e => e.Type == CalendarEventType.Deadline && e.PostingId == applied.Id);
    }

    [Fact]
    public async Task Never_returns_another_users_postings()
    {
        var other = AddPosting("Other", deadline: new DateOnly(2026, 12, 10), owner: Guid.NewGuid());
        other.SetDates(other.Deadline, new DateTimeOffset(2026, 12, 4, 9, 0, 0, TimeSpan.Zero));
        AddPosting("Mine", deadline: new DateOnly(2026, 12, 12));

        var events = await Handler().HandleAsync(From, To, default);

        Assert.Equal("Application deadline: Mine", Assert.Single(events).Title);
    }
}
