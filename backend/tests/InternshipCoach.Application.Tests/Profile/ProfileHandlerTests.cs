using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Profile;
using InternshipCoach.Application.Profile.Extract;
using InternshipCoach.Application.Profile.Get;
using InternshipCoach.Application.Profile.Items;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Tests.Profile;

public class ProfileHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    private const string CvText = "Jane Doe\nBuilt a REST API in C# with Docker.\nDutch: native, English: C1";

    private static Source CvSource()
    {
        var source = Source.Create(UserId, SourceKind.Cv, "cv.pdf");
        source.AddDocument("cv.pdf#page-1", CvText);
        source.MarkReady(Clock.UtcNow);
        return source;
    }

    private static Source GitHubSource()
    {
        var source = Source.Create(UserId, SourceKind.GitHub, "octocat");
        source.AddDocument("weather-app/summary", "Repository: weather-app\nPrimary language: C#\nDockerfile: yes");
        source.MarkReady(Clock.UtcNow);
        return source;
    }

    private static (ExtractProfileHandler Handler, FakeLlmTransport Transport, FakeProfileRepository Repo) Build(
        IEnumerable<Source> sources, params string[] replies)
    {
        var repo = new FakeProfileRepository(sources);
        var transport = new FakeLlmTransport(replies);
        var llm = new LlmClient(transport, new FakeLlmCallLog(), Clock, new LlmLimits());
        return (new ExtractProfileHandler(repo, llm, new FakeCurrentUser(UserId), Clock), transport, repo);
    }

    [Fact]
    public async Task Keeps_the_real_quote_and_drops_the_spliced_one()
    {
        var (handler, _, repo) = Build([CvSource()], """
            {"items":[
              {"doc":1,"kind":"skill","name":"Docker","quote":"REST API in C# with Docker"},
              {"doc":1,"kind":"skill","name":"Kubernetes","quote":"Deployed to Kubernetes with Docker"}
            ]}
            """);

        var result = await handler.HandleAsync(default);

        Assert.Equal(1, result.Created);
        Assert.Equal(1, result.Dropped);
        var item = Assert.Single(repo.Items);
        Assert.Equal("Docker", item.Name);
        Assert.Equal(UserId, item.UserId);
        Assert.True(item.Confirmed);
    }

    [Fact]
    public async Task Drops_items_with_a_missing_document_or_no_quote_match()
    {
        var (handler, _, repo) = Build([CvSource()], """
            {"items":[
              {"doc":5,"kind":"skill","name":"A","quote":"Docker"},
              {"doc":1,"kind":"skill","name":"B","quote":"Kotlin"},
              {"doc":1,"kind":"skill","name":"C","quote":"Jane Doe"}
            ]}
            """);

        var result = await handler.HandleAsync(default);

        Assert.Equal(2, result.Dropped);
        Assert.Equal("C", Assert.Single(repo.Items).Name);
    }

    [Fact]
    public async Task Cv_text_goes_to_the_smart_model_wrapped_as_untrusted_and_github_to_the_cheap_one()
    {
        var empty = """{"items":[]}""";
        var (handler, transport, _) = Build([CvSource(), GitHubSource()], empty, empty);

        await handler.HandleAsync(default);

        Assert.Equal(2, transport.Requests.Count);
        var cv = transport.Requests.Single(r => r.Model == LlmModel.Smart);
        var github = transport.Requests.Single(r => r.Model == LlmModel.Cheap);
        Assert.Contains("<untrusted_data name=\"doc1\">", cv.Turns[0].Content);
        Assert.Contains("Jane Doe", cv.Turns[0].Content);
        Assert.DoesNotContain("Jane Doe", github.Turns[0].Content);
        Assert.Contains("weather-app", github.Turns[0].Content);
    }

    [Fact]
    public async Task Same_kind_and_name_merges_into_one_item_and_keeps_both_quotes()
    {
        var (handler, _, repo) = Build([CvSource()], """
            {"items":[
              {"doc":1,"kind":"skill","name":"Docker","quote":"with Docker"},
              {"doc":1,"kind":"skill","name":"  docker ","quote":"Built a REST API"},
              {"doc":1,"kind":"language","name":"English","quote":"English: C1"}
            ]}
            """);

        var result = await handler.HandleAsync(default);

        Assert.Equal(2, result.Created);
        Assert.Equal(1, result.Merged);
        Assert.Equal(2, repo.Items.Single(i => i.Name == "Docker").Evidence.Count);
        Assert.All(repo.Items, item => Assert.True(item.Confirmed)); // extracted items count straight away
    }

    [Fact]
    public async Task Running_again_keeps_confirmed_items_and_adds_no_duplicates()
    {
        const string reply = """{"items":[{"doc":1,"kind":"skill","name":"Docker","quote":"with Docker"}]}""";
        var (handler, _, repo) = Build([CvSource()], reply, reply);

        await handler.HandleAsync(default);
        repo.Items.Single().SetConfirmed(true);
        var second = await handler.HandleAsync(default);

        Assert.Equal(0, second.Created);
        Assert.Equal(0, second.Merged);
        var item = Assert.Single(repo.Items);
        Assert.True(item.Confirmed);
        Assert.Single(item.Evidence);
    }

    [Fact]
    public async Task Without_any_ready_source_it_asks_for_one_and_calls_no_model()
    {
        var pending = Source.Create(UserId, SourceKind.Cv, "cv.pdf");
        var (handler, transport, _) = Build([pending]);

        await Assert.ThrowsAsync<ProfileInputException>(() => handler.HandleAsync(default));
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task Get_returns_items_with_their_source()
    {
        var cv = CvSource();
        var (handler, _, repo) = Build([cv], """{"items":[{"doc":1,"kind":"skill","name":"Docker","quote":"with Docker"}]}""");
        await handler.HandleAsync(default);

        var view = await new GetProfileHandler(repo).HandleAsync(default);
        var evidence = Assert.Single(Assert.Single(view.Items).Evidence);
        Assert.Equal("cv.pdf#page-1", evidence.DocumentPath);
        Assert.Equal(SourceKind.Cv, evidence.SourceKind);
    }

    [Fact]
    public async Task Delete_removes_an_item_and_refuses_an_unknown_one()
    {
        var repo = new FakeProfileRepository();
        var doc = Guid.NewGuid();
        var a = ProfileItem.Create(UserId, ProfileItemKind.Skill, "A", doc, "a", new CreatedAt(Clock.UtcNow));
        var b = ProfileItem.Create(UserId, ProfileItemKind.Skill, "B", doc, "b", new CreatedAt(Clock.UtcNow));
        repo.Items.AddRange([a, b]);

        Assert.False(await new DeleteItemHandler(repo).HandleAsync(Guid.NewGuid(), default));
        Assert.True(await new DeleteItemHandler(repo).HandleAsync(a.Id, default));
        Assert.Single(repo.Items);
    }
}
