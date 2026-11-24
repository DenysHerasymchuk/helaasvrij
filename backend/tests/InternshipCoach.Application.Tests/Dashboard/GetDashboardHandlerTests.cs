using InternshipCoach.Application.Applications;
using InternshipCoach.Application.Dashboard;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Tests.Dashboard;

public class GetDashboardHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeCurrentUser _user = new();
    private readonly FakeSourceRepository _sources = new();
    private readonly FakePostingRepository _postings = new();
    private readonly FakeApplicationRepository _applications;
    private readonly GetDashboardHandler _handler;

    public GetDashboardHandlerTests()
    {
        _applications = new FakeApplicationRepository(_postings);
        _handler = new GetDashboardHandler(_sources, _postings, new GetApplicationStatsHandler(_applications, new FakeClock(Now)));
    }

    private Posting AddPosting(params (string Text, Importance Importance)[] requirements)
    {
        var posting = Posting.Create(
            _user.UserId, "Acme", "Intern", "Gent", PostingLanguage.Nl, null, "text", new DateOnly(2026, 12, 12), requirements, false,
            new CreatedAt(Now));
        _postings.Items.Add(posting);
        return posting;
    }

    [Fact]
    public async Task New_user_gets_empty_lists_and_three_unconnected_sources()
    {
        var result = await _handler.HandleAsync(default);

        Assert.Empty(result.Postings);
        Assert.Equal(["GitHub", "CV", "Site"], result.Sources.Select(s => s.Kind));
        Assert.All(result.Sources, s => Assert.Null(s.Synced));
        Assert.Equal(GetApplicationStatsHandler.DefaultMonths, result.ApplicationsByMonth.Count);
        Assert.All(result.ApplicationsByMonth, m => Assert.Equal(0, m.Applied));
    }

    [Fact]
    public async Task Ready_source_shows_label_and_sync_time_but_failed_source_does_not()
    {
        var github = Source.Create(_user.UserId, SourceKind.GitHub, "lotte-dev");
        github.AddDocument("a", "text");
        github.MarkReady(Now);
        var cv = Source.Create(_user.UserId, SourceKind.Cv, "cv.pdf");
        cv.MarkFailed("bad pdf", Now);
        _sources.Items.AddRange([github, cv]);

        var result = await _handler.HandleAsync(default);

        Assert.Equal(new DashboardSource("GitHub", "lotte-dev", Now), result.Sources[0]);
        Assert.Equal(new DashboardSource("CV", "cv.pdf", null), result.Sources[1]);
        Assert.Equal(new DashboardSource("Site", "Not added yet", null), result.Sources[2]);
    }

    [Fact]
    public async Task Posting_carries_computed_score_requirements_and_gain()
    {
        var posting = AddPosting(("C#", Importance.Required), ("Docker", Importance.Required), ("CI", Importance.Nice));
        posting.Requirements[0].RecordAnalysis(Coverage.Strong, "q", Guid.NewGuid(), null, null, null, Now);
        posting.Requirements[1].RecordAnalysis(Coverage.Partial, "q", Guid.NewGuid(), "Add a Dockerfile", "budget-buddy", new DateOnly(2026, 11, 28), Now);
        posting.Requirements[2].RecordAnalysis(Coverage.None, null, null, "Add a build", "weather-app", null, Now);

        var result = await _handler.HandleAsync(default);

        var dto = Assert.Single(result.Postings);
        Assert.Equal("NL", dto.Language);
        Assert.Equal(posting.Score(), dto.Score);
        Assert.Equal(60, dto.Score); // (2 + 1) / 5
        Assert.Equal(["C#", "Docker", "CI"], dto.Requirements.Select(r => r.Text));
        Assert.Equal(new[] { 0, 20, 20 }, dto.Requirements.Select(r => r.Gain));
        Assert.Equal("budget-buddy", dto.Requirements[1].Project);
        Assert.Equal(new DateOnly(2026, 11, 28), dto.Requirements[1].Due);
    }

    [Fact]
    public async Task Posting_without_requirements_scores_zero()
    {
        AddPosting();

        var result = await _handler.HandleAsync(default);

        Assert.Equal(0, Assert.Single(result.Postings).Score);
    }

    [Fact]
    public async Task Applications_by_month_reuse_the_stats_logic()
    {
        var posting = AddPosting();
        var application = JobApplication.Create(_user.UserId, posting.Id, new CreatedAt(Now));
        application.MoveTo(ApplicationStage.Applied, Now);
        _applications.Items.Add(application);

        var result = await _handler.HandleAsync(default);

        Assert.Equal("Nov", result.ApplicationsByMonth[^1].Month);
        Assert.Equal(1, result.ApplicationsByMonth[^1].Applied);
    }
}
