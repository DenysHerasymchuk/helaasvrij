using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Interview;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Interview;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Tests.Interview;

public class InterviewHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));

    private sealed record Setup(
        GetInterviewHandler Get, SubmitAnswerHandler Submit, FakeLlmTransport Transport, FakeInterviewRepository Repo, Posting Posting, FakePostingRepository Postings);

    private static Setup Build(bool analysed, bool withRepo, params string[] replies)
    {
        var source = Source.Create(UserId, SourceKind.GitHub, "octocat");
        var doc = withRepo ? source.AddDocument("weather-app/README.md", "built a REST API in C#") : source.AddDocument("cv.pdf", "text");
        source.MarkReady(Clock.UtcNow);
        var profile = new FakeProfileRepository([source]);
        var item = ProfileItem.Create(UserId, ProfileItemKind.Skill, "C#", null, doc.Id, "built a REST API in C#", new CreatedAt(Clock.UtcNow));
        item.SetConfirmed(true);
        profile.Items.Add(item);

        var posting = Posting.Create(UserId, "Acme", "Intern", "Gent", PostingLanguage.En, null, "text", null,
            [("Docker", Importance.Required), ("Kubernetes", Importance.Nice)], false, new CreatedAt(Clock.UtcNow));
        if (analysed)
        {
            foreach (var r in posting.Requirements)
            {
                r.RecordAnalysis(Coverage.None, null, null, null, null, null, Clock.UtcNow);
            }
        }

        var postings = new FakePostingRepository();
        postings.Items.Add(posting);
        var transport = new FakeLlmTransport(replies);
        var llm = new LlmClient(transport, new FakeLlmCallLog(), Clock, new LlmLimits());
        var user = new FakeCurrentUser(UserId);
        var repo = new FakeInterviewRepository(UserId);
        var generator = new InterviewQuestionGenerator(llm, new GapEvidenceLoader(profile), user, Clock);
        return new Setup(new GetInterviewHandler(postings, repo, generator), new SubmitAnswerHandler(postings, repo, llm, user, Clock), transport, repo, posting, postings);
    }

    private const string Good = """
        {"gapQuestions":[{"req":1,"question":"How would you add Docker to weather-app?","project":"weather-app"},
                         {"req":2,"question":"How would you deploy ghost-app to Kubernetes?","project":"ghost-app"}],
         "repoQuestions":[{"question":"Why did you structure weather-app as a REST API?","project":"weather-app"},
                          {"question":"What would you change in other-repo?","project":"other-repo"}]}
        """;

    [Fact]
    public async Task Generates_once_keeps_only_questions_that_name_a_real_repo_and_stores_them()
    {
        var s = Build(true, true, Good, Good);

        var view = (await s.Get.HandleAsync(s.Posting.Id, default))!;

        Assert.All(view.Questions, q => Assert.Equal("weather-app", q.Project));
        Assert.Equal(2, view.Questions.Count);
        Assert.Equal(2, s.Transport.Requests.Count); // second call only asked for the missing questions, hard cap 2
        Assert.All(s.Transport.Requests, r => Assert.Equal(LlmModel.Smart, r.Model));
        Assert.All(s.Transport.Requests, r => Assert.Contains("<untrusted_data", r.Turns[0].Content));

        var again = await s.Get.HandleAsync(s.Posting.Id, default);
        Assert.Equal(2, again!.Questions.Count);
        Assert.Equal(2, s.Transport.Requests.Count); // no new LLM call: stored
    }

    [Fact]
    public async Task Unanalysed_posting_returns_no_questions_without_a_call_then_generates_after_analysis()
    {
        var s = Build(false, true, Good, Good);
        s.Repo.Company.Add(CompanyQuestion.Create("Acme", "Why Acme?"));

        var before = (await s.Get.HandleAsync(s.Posting.Id, default))!;
        Assert.False(before.Posting.IsAnalysed);
        Assert.Empty(before.Questions);
        Assert.Single(before.CompanyQuestions); // company questions can be practised already
        Assert.Empty(s.Transport.Requests);

        foreach (var r in s.Posting.Requirements)
        {
            r.RecordAnalysis(Coverage.None, null, null, null, null, null, Clock.UtcNow); // what POST /api/postings/{id}/analyse does
        }

        var after = (await s.Get.HandleAsync(s.Posting.Id, default))!;
        Assert.Equal(2, after.Questions.Count);
        Assert.Equal(2, s.Transport.Requests.Count);
    }

    [Fact]
    public async Task Regenerating_an_unanalysed_posting_is_refused_without_a_call()
    {
        var s = Build(false, true);
        await Assert.ThrowsAsync<InterviewInputException>(() => s.Get.HandleAsync(s.Posting.Id, default, regenerate: true));
        Assert.Empty(s.Transport.Requests);
    }

    [Fact]
    public async Task Regenerate_replaces_the_set_and_its_answers_but_keeps_company_answers()
    {
        var other = """
            {"gapQuestions":[{"req":1,"question":"Which base image would you pick for weather-app?","project":"weather-app"}],
             "repoQuestions":[{"question":"How do you test weather-app?","project":"weather-app"}]}
            """;
        var feedback = """{"score":3,"good":"a","add":"b","example":"c"}""";
        var s = Build(true, true, Good, Good, feedback, feedback, other, other);
        var company = CompanyQuestion.Create("Acme", "Why Acme?");
        s.Repo.Company.Add(company);
        var first = (await s.Get.HandleAsync(s.Posting.Id, default))!;
        await s.Submit.HandleAsync(new SubmitAnswerCommand(s.Posting.Id, first.Questions[0].Id, "Dockerfile."), default);
        await s.Submit.HandleAsync(new SubmitAnswerCommand(s.Posting.Id, company.Id, "Because."), default);

        var second = (await s.Get.HandleAsync(s.Posting.Id, default, regenerate: true))!;

        Assert.Equal(["Which base image would you pick for weather-app?", "How do you test weather-app?"], second.Questions.Select(q => q.Text));
        Assert.DoesNotContain(s.Repo.Questions, q => first.Questions.Any(f => f.Id == q.Id));
        Assert.Equal(company.Id, Assert.Single(second.Answers).QuestionId);
    }

    [Fact]
    public async Task Failed_regeneration_keeps_the_old_set()
    {
        var bad = """{"gapQuestions":[],"repoQuestions":[{"question":"About nothing","project":"ghost"}]}""";
        var s = Build(true, true, Good, Good, bad, bad);
        var first = (await s.Get.HandleAsync(s.Posting.Id, default))!;

        await Assert.ThrowsAsync<InterviewGenerationException>(() => s.Get.HandleAsync(s.Posting.Id, default, regenerate: true));

        Assert.Equal(first.Questions.Select(q => q.Id), s.Repo.Questions.Select(q => q.Id));
    }

    [Fact]
    public async Task Feedback_score_is_stored_and_an_out_of_range_score_is_dropped()
    {
        var s = Build(true, true, Good, Good, """{"score":4,"good":"a","add":"b","example":"c"}""", """{"good":"a","add":"b","example":"c"}""");
        var q = (await s.Get.HandleAsync(s.Posting.Id, default))!.Questions[0];

        Assert.Equal(4, (await s.Submit.HandleAsync(new SubmitAnswerCommand(s.Posting.Id, q.Id, "One."), default)).Answer!.Score);
        Assert.Null((await s.Submit.HandleAsync(new SubmitAnswerCommand(s.Posting.Id, q.Id, "Two."), default)).Answer!.Score);
        Assert.Null(InterviewAnswer.Create(UserId, s.Posting.Id, q.Id, "x", "a", "b", "c", new CreatedAt(Clock.UtcNow), 9).Score);
    }

    [Fact]
    public async Task Without_repos_builds_questions_from_gaps_and_confirmed_cv_items()
    {
        var reply = """
            {"gapQuestions":[{"req":1,"question":"How would you ship your C# API with Docker?","project":"C#"},
                             {"req":2,"question":"How would you deploy weather-app to Kubernetes?","project":"weather-app"}],
             "repoQuestions":[{"question":"What did you learn building a REST API in C#?","project":"C#"}]}
            """;
        var s = Build(true, false, reply, reply);

        var view = (await s.Get.HandleAsync(s.Posting.Id, default))!;

        Assert.Equal(2, view.Questions.Count); // the question naming an unknown repo is dropped
        Assert.All(view.Questions, q => Assert.Equal("C#", q.Project));
        Assert.Contains("<untrusted_data name=\"student_items\">", s.Transport.Requests[0].Turns[0].Content);
    }

    [Fact]
    public async Task Throws_when_no_question_is_usable()
    {
        var bad = """{"gapQuestions":[],"repoQuestions":[{"question":"About nothing","project":"ghost"}]}""";
        var s = Build(true, true, bad, bad);
        await Assert.ThrowsAsync<InterviewGenerationException>(() => s.Get.HandleAsync(s.Posting.Id, default));
        Assert.Empty(s.Repo.Questions);
    }

    [Fact]
    public async Task Another_users_posting_is_not_found_and_calls_nothing()
    {
        var s = Build(true, true);
        s.Postings.Items.Clear(); // the repository filter hides it
        Assert.Null(await s.Get.HandleAsync(s.Posting.Id, default));
        Assert.Empty(s.Transport.Requests);
        Assert.True((await s.Submit.HandleAsync(new SubmitAnswerCommand(s.Posting.Id, Guid.NewGuid(), "x"), default)).NotFound);
    }

    [Fact]
    public async Task Answer_gets_short_feedback_stored_with_one_call()
    {
        var s = Build(true, true, Good, Good, """{"good":"Clear.","add":"Mention tests.","example":"I added a Dockerfile to weather-app."}""");
        var view = (await s.Get.HandleAsync(s.Posting.Id, default))!;

        var result = await s.Submit.HandleAsync(new SubmitAnswerCommand(s.Posting.Id, view.Questions[0].Id, "I would write a Dockerfile."), default);

        Assert.Equal("Mention tests.", result.Answer!.WhatToAdd);
        Assert.Equal(UserId, result.Answer.UserId);
        Assert.Equal(3, s.Transport.Requests.Count);
        Assert.Contains("<untrusted_data name=\"answer\">", s.Transport.Requests[2].Turns[0].Content);
        Assert.Single(s.Repo.Answers);
    }

    [Fact]
    public async Task Company_question_can_be_practised_only_for_that_company_and_empty_answers_are_rejected()
    {
        var s = Build(true, true, """{"good":"a","add":"b","example":"c"}""");
        var mine = CompanyQuestion.Create("acme ", "Why Acme?");
        var other = CompanyQuestion.Create("Globex", "Why Globex?");
        s.Repo.Company.AddRange([mine, other]);

        Assert.NotNull((await s.Submit.HandleAsync(new SubmitAnswerCommand(s.Posting.Id, mine.Id, "Because."), default)).Answer);
        Assert.True((await s.Submit.HandleAsync(new SubmitAnswerCommand(s.Posting.Id, other.Id, "Because."), default)).NotFound);
        Assert.NotNull((await s.Submit.HandleAsync(new SubmitAnswerCommand(s.Posting.Id, mine.Id, "  "), default)).Error);
        Assert.Single(s.Transport.Requests);
    }

    [Fact]
    public void Company_questions_have_no_user_id()
    {
        Assert.DoesNotContain(typeof(CompanyQuestion).GetProperties(), p => p.Name.Contains("User"));
        Assert.False(typeof(IUserOwned).IsAssignableFrom(typeof(CompanyQuestion)));
    }
}
