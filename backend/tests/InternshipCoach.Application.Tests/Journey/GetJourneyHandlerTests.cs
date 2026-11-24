using InternshipCoach.Application.Journey;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Documents;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Tests.Journey;

public class GetJourneyHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeCurrentUser _user = new();
    private readonly FakeSourceRepository _sources = new();
    private readonly FakeProfileRepository _profile = new();
    private readonly FakePostingRepository _postings = new();
    private readonly FakeDocumentRepository _documents = new();
    private readonly FakeApplicationRepository _applications;
    private readonly GetJourneyHandler _handler;

    public GetJourneyHandlerTests()
    {
        _applications = new FakeApplicationRepository(_postings);
        _handler = new GetJourneyHandler(_sources, _profile, _postings, _documents, _applications);
    }

    private void AddReadySource()
    {
        var source = Source.Create(_user.UserId, SourceKind.GitHub, "lotte-dev");
        source.AddDocument("a", "text");
        source.MarkReady(Now);
        _sources.Items.Add(source);
    }

    private void AddConfirmedItem()
    {
        var item = ProfileItem.Create(_user.UserId, ProfileItemKind.Skill, "C#", null, Guid.NewGuid(), "C#", new CreatedAt(Now));
        item.SetConfirmed(true);
        _profile.Items.Add(item);
    }

    private Task SetStagePeriod() =>
        _profile.AddBasicsAsync(ProfileBasics.Create(_user.UserId, null, new DateOnly(2027, 2, 1), new DateOnly(2027, 5, 31)), default);

    private async Task CompleteProfile()
    {
        AddReadySource();
        AddConfirmedItem();
        await SetStagePeriod();
    }

    private Posting AddPosting(string company)
    {
        var posting = Posting.Create(
            _user.UserId, company, "Intern", "Gent", PostingLanguage.Nl, null, "text", null,
            [("C#", Importance.Required), ("Docker", Importance.Required)], false, new CreatedAt(Now));
        _postings.Items.Add(posting);
        return posting;
    }

    private static void Analyse(Posting posting, Coverage docker)
    {
        posting.Requirements[0].RecordAnalysis(Coverage.Strong, "q", Guid.NewGuid(), null, null, null, Now);
        var strong = docker == Coverage.Strong;
        posting.Requirements[1].RecordAnalysis(
            docker, docker == Coverage.None ? null : "q", docker == Coverage.None ? null : Guid.NewGuid(),
            strong ? null : "Add a Dockerfile", strong ? null : "weather-app", null, Now);
    }

    private void AddDocument(string title) =>
        _documents.Documents.Add(Document.Create(_user.UserId, DocumentKind.CoverLetter, "nl", title, "Hallo", new CreatedAt(Now)));

    private JobApplication AddToBoard(Posting posting)
    {
        var application = JobApplication.Create(_user.UserId, posting.Id, new CreatedAt(Now));
        _applications.Items.Add(application);
        return application;
    }

    [Fact]
    public async Task New_user_starts_by_adding_a_source()
    {
        var result = await _handler.HandleAsync(default);

        Assert.Equal(1, result.CurrentStep);
        Assert.False(result.HasSources);
        Assert.Null(result.BestPosting);
        Assert.Equal(new JourneyAction("Add your GitHub or CV", "/profile"), result.NextAction);
    }

    [Fact]
    public async Task Source_without_confirmed_items_asks_to_confirm()
    {
        AddReadySource();
        _profile.Items.Add(ProfileItem.Create(_user.UserId, ProfileItemKind.Skill, "C#", null, Guid.NewGuid(), "C#", new CreatedAt(Now)));

        var result = await _handler.HandleAsync(default);

        Assert.True(result.HasSources);
        Assert.Equal(0, result.ConfirmedItemCount);
        Assert.Equal("Confirm your profile items", result.NextAction.Label);
    }

    [Fact]
    public async Task Confirmed_items_without_stage_period_ask_for_it()
    {
        AddReadySource();
        AddConfirmedItem();

        var result = await _handler.HandleAsync(default);

        Assert.Equal(1, result.CurrentStep);
        Assert.Equal("Set your stage period", result.NextAction.Label);
    }

    [Fact]
    public async Task Complete_profile_without_postings_goes_to_find_a_posting()
    {
        await CompleteProfile();

        var result = await _handler.HandleAsync(default);

        Assert.Equal(2, result.CurrentStep);
        Assert.Equal(new JourneyAction("Find a posting", "/postings"), result.NextAction);
    }

    [Fact]
    public async Task Saved_posting_without_analysis_asks_to_analyse_it()
    {
        await CompleteProfile();
        var posting = AddPosting("Brightlane");

        var result = await _handler.HandleAsync(default);

        Assert.Equal(2, result.CurrentStep);
        Assert.Equal(1, result.SavedPostingCount);
        Assert.Equal(0, result.AnalysedPostingCount);
        Assert.Equal(new JourneyAction("Analyse Brightlane", $"/postings/{posting.Id}"), result.NextAction);
    }

    [Fact]
    public async Task Analysed_posting_with_open_gap_asks_to_close_it_and_picks_highest_score()
    {
        await CompleteProfile();
        Analyse(AddPosting("Lowscore"), Coverage.None);
        var best = AddPosting("Brightlane");
        Analyse(best, Coverage.Partial);

        var result = await _handler.HandleAsync(default);

        Assert.Equal(3, result.CurrentStep);
        Assert.Equal(new JourneyPosting(best.Id, "Brightlane", "Intern", 75), result.BestPosting);
        Assert.Equal(2, result.OpenGapCount);
        Assert.Equal(new JourneyAction("Close your top gap", "/gaps"), result.NextAction);
    }

    [Fact]
    public async Task Ticked_gap_moves_on_to_tailoring_the_cv()
    {
        await CompleteProfile();
        var best = AddPosting("Brightlane");
        Analyse(best, Coverage.Partial);
        best.Requirements[1].MarkDone();

        var result = await _handler.HandleAsync(default);

        Assert.Equal(4, result.CurrentStep);
        Assert.Equal(0, result.OpenGapCount);
        Assert.Equal(new JourneyAction("Tailor your CV for Brightlane", $"/documents?postingId={best.Id}"), result.NextAction);
    }

    [Fact]
    public async Task Document_for_best_posting_asks_to_add_it_to_applications()
    {
        await CompleteProfile();
        var best = AddPosting("Brightlane");
        Analyse(best, Coverage.Strong);
        AddDocument("Cover letter - Brightlane");
        AddDocument("Cover letter - Other");

        var result = await _handler.HandleAsync(default);

        Assert.Equal(1, result.DocumentCountForBest);
        Assert.Equal(new JourneyAction("Add Brightlane to applications", $"/postings/{best.Id}"), result.NextAction);
    }

    [Fact]
    public async Task Saved_on_board_asks_to_mark_it_applied()
    {
        await CompleteProfile();
        var best = AddPosting("Brightlane");
        Analyse(best, Coverage.Strong);
        AddDocument("Cover letter - Brightlane");
        AddToBoard(best);

        var result = await _handler.HandleAsync(default);

        Assert.True(result.BestOnBoard);
        Assert.Equal(1, result.OnBoardCount);
        Assert.Equal(0, result.AppliedCount);
        Assert.False(result.Complete);
        Assert.Equal(new JourneyAction("Mark it applied", "/applications"), result.NextAction);
    }

    [Fact]
    public async Task Applied_best_posting_completes_the_journey()
    {
        await CompleteProfile();
        var best = AddPosting("Brightlane");
        Analyse(best, Coverage.Strong);
        AddDocument("Cover letter - Brightlane");
        AddToBoard(best).MoveTo(ApplicationStage.Applied, Now);

        var result = await _handler.HandleAsync(default);

        Assert.Equal(4, result.CurrentStep);
        Assert.True(result.Complete);
        Assert.Equal(1, result.AppliedCount);
        Assert.Equal("/interview", result.NextAction.Href);
    }
}
