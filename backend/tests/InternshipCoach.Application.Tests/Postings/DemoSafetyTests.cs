using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Application.Postings.Generic;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Tests.Postings;

public class DemoSafetyTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    private const string Reply = """{"rows":[{"req":1,"coverage":"strong","quote":"shipped it with Docker"},{"req":2,"coverage":"none","action":"Add tests to weather-app","project":"weather-app"}]}""";

    private static (AnalysePostingHandler Handler, FakePostingRepository Postings, Posting Posting, OutageLlmTransport Transport) Build(
        bool isDemo, DemoSafetyOptions options, bool hang, params string[] replies)
    {
        var source = Source.Create(UserId, SourceKind.GitHub, "octocat");
        var doc = source.AddDocument("weather-app/README.md", "weather-app: shipped it with Docker.");
        source.MarkReady(Clock.UtcNow);
        var profile = new FakeProfileRepository([source]);
        var item = ProfileItem.Create(UserId, ProfileItemKind.Skill, "Docker", doc.Id, "shipped it with Docker", new CreatedAt(Clock.UtcNow));
        item.SetConfirmed(true);
        profile.Items.Add(item);

        var posting = Posting.Create(UserId, "Brightlane", "Intern", "Mechelen", PostingLanguage.En, null, "Demo data. Docker and tests.", null,
            [("Docker", Importance.Required), ("Automated tests", Importance.Required)], isDemo, new CreatedAt(Clock.UtcNow));
        var postings = new FakePostingRepository();
        postings.Items.Add(posting);

        var transport = new OutageLlmTransport(hang, replies);
        var llm = new LlmClient(transport, new FakeLlmCallLog(), Clock, new LlmLimits());
        var handler = new AnalysePostingHandler(postings, new GapEvidenceLoader(profile), new GapAnalyser(llm, new FakeCurrentUser(UserId)), Clock, options);
        return (handler, postings, posting, transport);
    }

    [Fact]
    public async Task Demo_posting_keeps_its_stored_analysis_when_the_model_is_down()
    {
        var s = Build(isDemo: true, new DemoSafetyOptions(Enabled: true), hang: false, Reply);
        var first = (await s.Handler.HandleAsync(s.Posting.Id, default))!;
        Assert.False(first.FromDemoCache);
        Assert.Equal(50, first.Posting.Score());
        var saves = s.Postings.Saves;

        var second = (await s.Handler.HandleAsync(s.Posting.Id, default))!;

        Assert.True(second.FromDemoCache);
        Assert.Equal(50, second.Posting.Score());
        Assert.Equal("shipped it with Docker", second.Posting.Requirements[0].EvidenceQuote);
        Assert.Single(second.Posting.Snapshots); // no new snapshot, nothing saved
        Assert.Equal(saves, s.Postings.Saves);
    }

    [Fact]
    public async Task Demo_posting_keeps_its_stored_analysis_when_the_model_is_too_slow()
    {
        var s = Build(isDemo: true, new DemoSafetyOptions(Enabled: true, TimeoutSeconds: 1), hang: true, Reply);
        await s.Handler.HandleAsync(s.Posting.Id, default);

        var second = (await s.Handler.HandleAsync(s.Posting.Id, default))!;

        Assert.True(second.FromDemoCache);
        Assert.Equal(50, second.Posting.Score());
    }

    [Fact]
    public async Task Non_demo_posting_never_falls_back()
    {
        var s = Build(isDemo: false, new DemoSafetyOptions(Enabled: true), hang: false, Reply);
        await s.Handler.HandleAsync(s.Posting.Id, default);

        await Assert.ThrowsAsync<LlmUnavailableException>(() => s.Handler.HandleAsync(s.Posting.Id, default));
    }

    [Fact]
    public async Task Demo_posting_without_a_stored_analysis_fails_instead_of_inventing_one()
    {
        var s = Build(isDemo: true, new DemoSafetyOptions(Enabled: true), hang: false);
        await Assert.ThrowsAsync<LlmUnavailableException>(() => s.Handler.HandleAsync(s.Posting.Id, default));
        Assert.False(s.Posting.IsAnalysed);
    }

    [Fact]
    public async Task Fallback_is_off_by_default()
    {
        var s = Build(isDemo: true, new DemoSafetyOptions(), hang: false, Reply);
        await s.Handler.HandleAsync(s.Posting.Id, default);

        await Assert.ThrowsAsync<LlmUnavailableException>(() => s.Handler.HandleAsync(s.Posting.Id, default));
    }
}

public class GenericAnswerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

    private static Posting NewPosting(bool isDemo) =>
        Posting.Create(UserId, "Acme", "Intern", "Gent", PostingLanguage.En, null, "We want Python and SQL.", null,
            [("Python", Importance.Required)], isDemo, new CreatedAt(Clock.UtcNow));

    [Fact]
    public async Task Generates_once_with_the_cheap_model_from_the_posting_text_only_and_caches_it()
    {
        var posting = NewPosting(false);
        var postings = new FakePostingRepository();
        postings.Items.Add(posting);
        var transport = new FakeLlmTransport("Learn Python and SQL. Good luck!");
        var handler = new GenericAnswerHandler(postings, new LlmClient(transport, new FakeLlmCallLog(), Clock, new LlmLimits()),
            new FakeCurrentUser(UserId), Clock);

        var first = (await handler.HandleAsync(posting.Id, default))!;
        var second = (await handler.HandleAsync(posting.Id, default))!;

        Assert.Equal("Learn Python and SQL. Good luck!", first.GenericAnswer);
        Assert.Equal(Clock.UtcNow, first.GenericAnswerAt);
        Assert.Same(first, second);
        var request = Assert.Single(transport.Requests); // cached: no second call
        Assert.Equal(LlmModel.Cheap, request.Model);
        var user = Assert.Single(request.Turns).Content;
        Assert.Equal(UntrustedInput.Wrap("posting", "We want Python and SQL."), user); // posting text and nothing else
    }

    [Fact]
    public async Task Stored_answer_is_returned_without_a_model_call()
    {
        var posting = NewPosting(true);
        posting.SetGenericAnswer("Stored generic answer.", Clock.UtcNow);
        var postings = new FakePostingRepository();
        postings.Items.Add(posting);
        var transport = new FakeLlmTransport();
        var handler = new GenericAnswerHandler(postings, new LlmClient(transport, new FakeLlmCallLog(), Clock, new LlmLimits()),
            new FakeCurrentUser(UserId), Clock);

        var result = (await handler.HandleAsync(posting.Id, default))!;

        Assert.Equal("Stored generic answer.", result.GenericAnswer);
        Assert.Empty(transport.Requests);
        Assert.Null(await handler.HandleAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public void Empty_generic_answer_is_rejected_and_long_ones_are_clipped()
    {
        var posting = NewPosting(false);
        Assert.Throws<DomainException>(() => posting.SetGenericAnswer("  ", Clock.UtcNow));
        posting.SetGenericAnswer(new string('a', Posting.GenericAnswerMaxLength + 50), Clock.UtcNow);
        Assert.Equal(Posting.GenericAnswerMaxLength, posting.GenericAnswer!.Length);
    }
}
