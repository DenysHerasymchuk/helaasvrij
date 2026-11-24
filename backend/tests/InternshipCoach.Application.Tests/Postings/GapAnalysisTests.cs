using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Tests.Postings;

public class GapAnalysisTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    private const string Readme = "weather-app: built a REST API in C# and shipped it with Docker.";

    private sealed record Setup(
        AnalysePostingHandler Handler, FakeLlmTransport Transport, FakePostingRepository Postings, Posting Posting, FakeProfileRepository Profile);

    private static Setup Build(bool confirm, params string[] replies) => BuildAt(confirm, "weather-app/README.md", replies);

    private static Setup BuildAt(bool confirm, string docPath, params string[] replies)
    {
        var source = Source.Create(UserId, SourceKind.GitHub, "octocat");
        var doc = source.AddDocument(docPath, Readme);
        source.MarkReady(Clock.UtcNow);

        var profile = new FakeProfileRepository([source]);
        var item = ProfileItem.Create(UserId, ProfileItemKind.Skill, "Docker", null, doc.Id, "shipped it with Docker", new CreatedAt(Clock.UtcNow));
        item.SetConfirmed(confirm);
        profile.Items.Add(item);

        var posting = Posting.Create(UserId, "Acme", "Intern", "Gent", PostingLanguage.En, null, "text", null,
            [("Docker experience", Importance.Required), ("Kubernetes", Importance.Required), ("Testing with xUnit", Importance.Nice)],
            false, new CreatedAt(Clock.UtcNow));
        var postings = new FakePostingRepository();
        postings.Items.Add(posting);

        var transport = new FakeLlmTransport(replies);
        var llm = new LlmClient(transport, new FakeLlmCallLog(), Clock, new LlmLimits());
        var handler = new AnalysePostingHandler(postings, new GapEvidenceLoader(profile), new GapAnalyser(llm, new FakeCurrentUser(UserId)), Clock, new DemoSafetyOptions());
        return new Setup(handler, transport, postings, posting, profile);
    }

    // Row 1: real quote. Row 2: spliced quote (not in any evidence), action names a repo that does not exist.
    // Row 3: none, action names a real repo.
    private const string FirstReply = """
        {"rows":[
          {"req":1,"coverage":"strong","quote":"shipped it with Docker"},
          {"req":2,"coverage":"strong","quote":"shipped it with Kubernetes","action":"Deploy ghost-project to Kubernetes","project":"ghost-project"},
          {"req":3,"coverage":"none","action":"Add xUnit tests to weather-app","project":"weather-app"}
        ]}
        """;

    [Fact]
    public async Task Keeps_real_quote_downgrades_spliced_one_and_regenerates_a_bad_action_once()
    {
        var s = Build(true, FirstReply, """{"actions":[{"req":1,"action":"Add a k8s manifest to weather-app","project":"weather-app"}]}""");

        var posting = (await s.Handler.HandleAsync(s.Posting.Id, default))!.Posting;
        var reqs = posting.Requirements.OrderBy(r => r.Position).ToList();

        Assert.Equal(Coverage.Strong, reqs[0].Coverage);
        Assert.Equal("shipped it with Docker", reqs[0].EvidenceQuote);
        Assert.NotNull(reqs[0].EvidenceSourceDocumentId);
        Assert.Null(reqs[0].Action);

        Assert.Equal(Coverage.None, reqs[1].Coverage);
        Assert.Null(reqs[1].EvidenceQuote);
        Assert.Equal("weather-app", reqs[1].ActionProject);
        Assert.Contains("weather-app", reqs[1].Action);

        Assert.Equal("weather-app", reqs[2].ActionProject);
        Assert.Equal(2, s.Transport.Requests.Count);
        Assert.All(reqs, r => Assert.NotNull(r.AnalysedAt));
        Assert.Equal(40, posting.Score()); // (2*1) / (2+2+1)
    }

    [Fact]
    public async Task Leaves_the_action_empty_when_the_second_try_also_names_no_real_repo()
    {
        var s = Build(true, FirstReply, """{"actions":[{"req":1,"action":"Deploy ghost-project","project":"ghost-project"}]}""");

        var posting = (await s.Handler.HandleAsync(s.Posting.Id, default))!.Posting;
        var kube = posting.Requirements.Single(r => r.Text == "Kubernetes");

        Assert.Null(kube.Action);
        Assert.Null(kube.ActionProject);
        Assert.Equal(2, s.Transport.Requests.Count); // hard cap: never a third call
    }

    [Fact]
    public async Task Rejects_an_action_that_does_not_mention_its_project()
    {
        var reply = """{"rows":[{"req":1,"coverage":"none","action":"Learn Kubernetes","project":"weather-app"}]}""";
        var s = Build(true, reply, """{"actions":[{"req":1,"action":"Learn Kubernetes","project":"weather-app"}]}""");

        var posting = (await s.Handler.HandleAsync(s.Posting.Id, default))!.Posting;
        Assert.All(posting.Requirements.Where(r => r.Coverage == Coverage.None), r => Assert.Null(r.Action));
    }

    // No repo: "cv.txt" has no "repo/" prefix, so the student has only CV evidence.
    [Fact]
    public async Task Without_repos_keeps_a_concrete_new_project_and_regenerates_vague_ones_once()
    {
        var first = """
            {"rows":[
              {"req":1,"coverage":"strong","quote":"shipped it with Docker"},
              {"req":2,"coverage":"none","action":"Start `k8s-weather-deploy`: deploy a small API to Kubernetes with a Helm chart","project":"k8s-weather-deploy"},
              {"req":3,"coverage":"none","action":"Start `my-project`: gain more experience","project":"my-project"}
            ]}
            """;
        var retry = """{"actions":[{"req":1,"action":"Start `Testing_App`: write xUnit tests","project":"Testing_App"}]}""";
        var s = BuildAt(true, "cv.txt", first, retry);

        var posting = (await s.Handler.HandleAsync(s.Posting.Id, default))!.Posting;
        var reqs = posting.Requirements.OrderBy(r => r.Position).ToList();

        Assert.Equal("new:k8s-weather-deploy", reqs[1].ActionProject);
        Assert.StartsWith("Start `k8s-weather-deploy`", reqs[1].Action);
        Assert.Null(reqs[2].Action); // vague first try, not kebab-case on the retry
        Assert.Equal(2, s.Transport.Requests.Count);
        Assert.DoesNotContain("<untrusted_data name=\"repos\">", s.Transport.Requests[1].Turns[0].Content);
        Assert.Equal(40, posting.Score()); // score logic unchanged
    }

    [Fact]
    public async Task Without_repos_a_regenerated_project_idea_is_kept()
    {
        var first = """{"rows":[{"req":1,"coverage":"strong","quote":"shipped it with Docker"},{"req":2,"coverage":"none"},{"req":3,"coverage":"none"}]}""";
        var retry = """
            {"actions":[
              {"req":1,"action":"Start `helm-starter`: package a demo app","project":"helm-starter"},
              {"req":2,"action":"Start `expense-tracker-api`: build a REST API with ASP.NET Core and add xUnit tests","project":"expense-tracker-api"}
            ]}
            """;
        var s = BuildAt(true, "cv.txt", first, retry);

        var posting = (await s.Handler.HandleAsync(s.Posting.Id, default))!.Posting;

        Assert.Null(posting.Requirements.Single(r => r.Text == "Kubernetes").Action); // does not mention Kubernetes
        Assert.Equal("new:expense-tracker-api", posting.Requirements.Single(r => r.Text == "Testing with xUnit").ActionProject);
    }

    [Fact]
    public async Task Stores_a_snapshot_and_spreads_due_dates_on_the_open_gaps()
    {
        var s = Build(true, FirstReply, """{"actions":[{"req":1,"action":"Add a k8s manifest to weather-app","project":"weather-app"}]}""");
        var posting = (await s.Handler.HandleAsync(s.Posting.Id, default))!.Posting;

        Assert.Equal("analyse", Assert.Single(posting.Snapshots).Reason);
        var strong = posting.Requirements.Single(r => r.Coverage == Coverage.Strong);
        Assert.Null(strong.DueDate);
        var today = new DateOnly(2026, 11, 24);
        var dues = posting.Requirements.Where(r => r.DueDate is not null).Select(r => r.DueDate!.Value).OrderBy(d => d).ToList();
        Assert.Equal([today.AddDays(3), today.AddDays(21)], dues);
        Assert.Equal(today.AddDays(3), posting.Requirements.Single(r => r.Text == "Kubernetes").DueDate); // higher impact first
    }

    [Fact]
    public async Task Without_confirmed_items_nothing_is_sent_to_the_model()
    {
        var s = Build(false);
        await Assert.ThrowsAsync<GapInputException>(() => s.Handler.HandleAsync(s.Posting.Id, default));
        Assert.Empty(s.Transport.Requests);
    }

    [Fact]
    public async Task User_text_is_wrapped_as_untrusted()
    {
        var s = Build(true, """{"rows":[]}""", """{"actions":[]}""");
        await s.Handler.HandleAsync(s.Posting.Id, default);

        var user = s.Transport.Requests[0].Turns[0].Content;
        Assert.Contains("<untrusted_data name=\"requirements\">", user);
        Assert.Contains("<untrusted_data name=\"evidence\">", user);
        Assert.Contains("<untrusted_data name=\"repos\">", user);
        Assert.Equal(LlmModel.Smart, s.Transport.Requests[0].Model);
    }

    [Fact]
    public async Task Recheck_updates_one_requirement_and_stores_a_rescan_snapshot()
    {
        var s = Build(true, FirstReply, """{"actions":[{"req":1,"action":"Add a k8s manifest to weather-app","project":"weather-app"}]}""",
            """{"rows":[{"req":1,"coverage":"partial","quote":"shipped it with Docker","action":"Add a k8s manifest to weather-app","project":"weather-app"}]}""");
        var posting = (await s.Handler.HandleAsync(s.Posting.Id, default))!.Posting;
        var kube = posting.Requirements.Single(r => r.Text == "Kubernetes");
        var due = kube.DueDate;

        var llm = new LlmClient(s.Transport, new FakeLlmCallLog(), Clock, new LlmLimits());
        var recheck = new RecheckRequirementHandler(s.Postings, new GapEvidenceLoader(s.Profile), new GapAnalyser(llm, new FakeCurrentUser(UserId)), Clock);
        await recheck.HandleAsync(posting.Id, kube.Id, default);

        Assert.Equal(Coverage.Partial, kube.Coverage);
        Assert.Equal(due, kube.DueDate);
        Assert.Equal(["analyse", "rescan"], posting.Snapshots.Select(x => x.Reason));
    }

    [Fact]
    public void Due_dates_for_a_single_gap_is_a_week_out()
    {
        var posting = Posting.Create(UserId, "A", "B", null, PostingLanguage.En, null, "t", null, [("x", Importance.Nice)], false, new CreatedAt(Clock.UtcNow));
        var dues = GapPlan.DueDates([(posting.Requirements[0], Coverage.None)], new DateOnly(2026, 11, 24));
        Assert.Equal(new DateOnly(2026, 12, 1), dues[posting.Requirements[0].Id]);
    }
}
