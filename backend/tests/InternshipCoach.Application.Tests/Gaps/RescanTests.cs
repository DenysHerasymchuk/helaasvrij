using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Gaps;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Application.Profile.Extract;
using InternshipCoach.Application.Sources;
using InternshipCoach.Application.Sources.GitHub;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Tests.Gaps;

public class RescanTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    private const string Readme = "weather-app: a REST API in C#.";

    private static GitHubRepoData Repo(bool dockerfile) =>
        new("weather-app", "https://github.com/octocat/weather-app", "Forecasts", [], "C#", new Dictionary<string, long>(),
            Readme, ["src"], dockerfile, false, false, 10, null);

    private sealed record Setup(
        MarkGapDoneHandler Handler, FakeProfileRepository Profile, FakeSourceRepository Sources, FakeGitHubClient GitHub,
        Posting Posting, Requirement Requirement, ProfileItem ConfirmedFromOldSummary, ProfileItem ConfirmedFromReadme,
        FakeLlmTransport Transport, RescanThrottle Throttle, Guid OldSummaryId);

    private static Setup Build(params string[] replies)
    {
        var source = Source.Create(UserId, SourceKind.GitHub, "octocat");
        GitHubDocumentBuilder.AddDocuments(source, [Repo(false)]);
        source.MarkReady(Clock.UtcNow);
        var readme = source.Documents.Single(d => d.Path.EndsWith("README.md"));
        var summary = source.Documents.Single(d => d.Path.EndsWith("summary"));

        var sources = new FakeSourceRepository();
        sources.Items.Add(source);
        var profile = new FakeProfileRepository([source]);

        var fromReadme = ProfileItem.Create(UserId, ProfileItemKind.Skill, "Docker", readme.Id, "a REST API in C#", new CreatedAt(Clock.UtcNow));
        fromReadme.SetConfirmed(true);
        var fromSummary = ProfileItem.Create(UserId, ProfileItemKind.Project, "weather-app", summary.Id, "Dockerfile: no", new CreatedAt(Clock.UtcNow));
        fromSummary.SetConfirmed(true);
        profile.Items.AddRange([fromReadme, fromSummary]);

        var posting = Posting.Create(UserId, "Brightlane", "Intern", "Gent", PostingLanguage.En, null, "text", null,
            [("Docker experience", Importance.Required)], false, new CreatedAt(Clock.UtcNow));
        var requirement = posting.Requirements[0];
        requirement.RecordAnalysis(Coverage.Partial, "a REST API in C#", readme.Id, "Add a Dockerfile to weather-app", "weather-app",
            new DateOnly(2026, 12, 1), Clock.UtcNow);
        var postings = new FakePostingRepository();
        postings.Items.Add(posting);

        var github = new FakeGitHubClient { Fresh = Repo(true) };
        var transport = new FakeLlmTransport(replies);
        var llm = new LlmClient(transport, new FakeLlmCallLog(), Clock, new LlmLimits());
        var user = new FakeCurrentUser(UserId);
        var extract = new ExtractProfileHandler(profile, llm, user, Clock);
        var recheck = new RecheckRequirementHandler(postings, new GapEvidenceLoader(profile), new GapAnalyser(llm, user), Clock);
        var throttle = new RescanThrottle();
        var handler = new MarkGapDoneHandler(postings, sources, profile, github, extract, recheck, throttle, Clock, user);
        return new Setup(handler, profile, sources, github, posting, requirement, fromSummary, fromReadme, transport, throttle, summary.Id);
    }

    private const string Extraction = """{"items":[{"doc":1,"kind":"skill","name":"Docker","quote":"Dockerfile: yes"},{"doc":1,"kind":"skill","name":"Dockerfile","quote":"Dockerfile: yes"}]}""";
    private const string Label = """{"rows":[{"req":1,"coverage":"strong","quote":"Dockerfile: yes"}]}""";

    [Fact]
    public async Task Rescan_keeps_confirmed_items_and_their_evidence_and_raises_the_score()
    {
        var s = Build(Extraction, Label);
        var result = (await s.Handler.HandleAsync(s.Requirement.Id, default))!;

        Assert.Equal(50, result.OldScore);
        Assert.Equal(100, result.NewScore);
        Assert.Equal(Coverage.Strong, s.Requirement.Coverage);
        Assert.True(s.Requirement.Done);

        // Confirmed items survive, still confirmed, evidence still points at documents that exist and still verify.
        Assert.Contains(s.ConfirmedFromReadme, s.Profile.Items);
        Assert.Contains(s.ConfirmedFromOldSummary, s.Profile.Items);
        Assert.True(s.ConfirmedFromReadme.Confirmed && s.ConfirmedFromOldSummary.Confirmed);
        var source = s.Sources.Items.Single();
        Assert.Same(source, s.Sources.Items.Single()); // source not replaced
        var docs = source.Documents.ToDictionary(d => d.Id);
        foreach (var e in s.Profile.Items.SelectMany(i => i.Evidence))
        {
            Assert.True(QuoteCheck.IsVerbatim(e.Quote, docs[e.SourceDocumentId].Text));
        }

        Assert.Contains(s.OldSummaryId, docs.Keys); // referenced by confirmed evidence, so kept
        Assert.Contains(docs.Values, d => d.Text.Contains("Dockerfile: yes"));
        Assert.Equal(0, result.UnverifiableEvidence);
        Assert.Equal(1, result.NewItemsToReview); // "Dockerfile" is new and unconfirmed
        Assert.True(s.Profile.Items.Single(i => i.Name == "Dockerfile").Confirmed);
        Assert.StartsWith("Re-scan: Add a Dockerfile", s.Posting.Snapshots.Single().Reason);
    }

    [Fact]
    public async Task Ticking_a_new_project_action_marks_it_done_without_rescan_or_score_change()
    {
        var s = Build();
        s.Requirement.RecordAnalysis(Coverage.None, null, null, "Start `docker-demo-api`: containerise it with Docker",
            "new:docker-demo-api", new DateOnly(2026, 12, 1), Clock.UtcNow);
        s.GitHub.FailWith = "must not be called";

        var result = (await s.Handler.HandleAsync(s.Requirement.Id, default))!;

        Assert.True(s.Requirement.Done);
        Assert.Equal(result.OldScore, result.NewScore);
        Assert.Equal(Coverage.None, result.NewCoverage);
        Assert.Empty(s.Transport.Requests);
        Assert.Empty(s.Posting.Snapshots);
    }

    [Fact]
    public async Task Rescan_is_limited_to_one_per_requirement_per_minute()
    {
        var s = Build(Extraction, Label);
        s.Throttle.TryAcquire(UserId, s.Requirement.Id, Clock.UtcNow);
        var ex = await Assert.ThrowsAsync<RescanThrottledException>(() => s.Handler.HandleAsync(s.Requirement.Id, default));
        Assert.Equal(60, ex.RetryAfterSeconds);
        Assert.Empty(s.Transport.Requests);
        Assert.Equal(0, s.Throttle.TryAcquire(UserId, s.Requirement.Id, Clock.UtcNow.AddSeconds(61)));
    }

    [Fact]
    public async Task A_failed_github_fetch_changes_nothing()
    {
        var s = Build();
        s.GitHub.FailWith = "GitHub rate limit reached. Try again later.";
        await Assert.ThrowsAsync<SourceFetchException>(() => s.Handler.HandleAsync(s.Requirement.Id, default));
        Assert.False(s.Requirement.Done);
        Assert.Equal(Coverage.Partial, s.Requirement.Coverage);
    }

    [Fact]
    public async Task Unknown_or_already_done_requirements_are_refused()
    {
        var s = Build();
        Assert.Null(await s.Handler.HandleAsync(Guid.NewGuid(), default));
        s.Requirement.MarkDone();
        await Assert.ThrowsAsync<GapInputException>(() => s.Handler.HandleAsync(s.Requirement.Id, default));
    }

    [Fact]
    public async Task List_sorts_open_gaps_by_impact_and_undo_unticks()
    {
        var s = Build();
        var postings = new FakePostingRepository();
        postings.Items.Add(s.Posting);
        var rows = await new ListGapsHandler(postings).HandleAsync(default);
        var row = Assert.Single(rows);
        Assert.Equal(1m, row.Impact); // required (2) x half missing
        Assert.Equal(50, row.ScoreGain);

        s.Requirement.MarkDone();
        Assert.True((await new UndoGapHandler(postings).HandleAsync(s.Requirement.Id, default))!.Done == false);
        Assert.Null(await new UndoGapHandler(postings).HandleAsync(Guid.NewGuid(), default));
    }
}
