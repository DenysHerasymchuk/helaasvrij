using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Settings.Update;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Application.Writing;
using InternshipCoach.Domain.Settings;

namespace InternshipCoach.Application.Tests.Writing;

public class WritingSkillsTests
{
    private const string Schema = """{"type":"object","required":["name"],"properties":{"name":{"type":"string"}}}""";
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));

    private readonly FakeSettingsRepository _settings = new();
    private readonly FakeLlmTransport _transport = new("""{"name":"x"}""", """{"name":"x"}""", """{"name":"x"}""", """{"name":"x"}""");

    private StyledLlmClient Client() => new(new LlmClient(_transport, new FakeLlmCallLog(), Clock, new LlmLimits()), _settings);

    private async Task<string> SystemFor(string feature)
    {
        await Client().CompleteJsonAsync<Dictionary<string, string>>(new LlmCallContext(UserId, feature), LlmModel.Smart, "base", "user", Schema, 500, default);
        return _transport.Requests[^1].System;
    }

    [Fact]
    public async Task Documents_use_the_humanizer_by_default_and_the_teacher_is_not_added()
    {
        var system = await SystemFor("cv-tailor");

        Assert.Contains("Humanizer", system);
        Assert.DoesNotContain("Teacher", system);
    }

    [Fact]
    public async Task Interview_questions_and_feedback_use_the_humanizer_and_the_teacher_by_default()
    {
        Assert.Contains("Teacher", await SystemFor("interview-questions"));
        var feedback = await SystemFor("interview-feedback");
        Assert.Contains("Humanizer", feedback);
        Assert.Contains("Teacher", feedback);
    }

    [Theory]
    [InlineData("gap-analyse")]
    [InlineData("profile-extract")]
    [InlineData("chat")]
    public async Task Calls_that_do_not_write_for_the_user_get_no_skills(string feature)
    {
        Assert.DoesNotContain("Writing skills", await SystemFor(feature));
    }

    [Fact]
    public async Task Saved_choices_replace_the_defaults_and_an_empty_list_means_none()
    {
        var row = UserSettings.Create(UserId, "en", 75, false);
        row.SetWritingSkills(["concise"], []);
        _settings.Rows.Add(row);

        var documents = await SystemFor("cover-letter");
        var interview = await SystemFor("interview-questions");

        Assert.Contains("Concise", documents);
        Assert.DoesNotContain("Humanizer", documents);
        Assert.DoesNotContain("Writing skills", interview);
    }

    [Fact]
    public async Task The_brief_tells_the_model_not_to_touch_facts_or_the_json()
    {
        var system = await SystemFor("cv-create");

        Assert.Contains("Never change facts", system);
    }

    [Fact]
    public async Task Update_saves_skills_and_rejects_unknown_or_too_many()
    {
        var handler = new UpdateSettingsHandler(_settings, new FakeCurrentUser(UserId));

        var ok = await handler.HandleAsync(new UpdateSettingsCommand("en", 75, false, ["formal"], ["teacher", "concise"]), default);
        var unknown = await handler.HandleAsync(new UpdateSettingsCommand("en", 75, false, ["poet"], null), default);
        var many = await handler.HandleAsync(new UpdateSettingsCommand("en", 75, false, null, ["humanizer", "teacher", "concise", "formal", "x"]), default);

        Assert.Equal(["formal"], ok.DocumentSkills);
        Assert.Equal(["teacher", "concise"], ok.InterviewSkills);
        Assert.Contains("documentSkills", unknown.Errors!.Keys);
        Assert.Contains("interviewSkills", many.Errors!.Keys);
    }

    [Fact]
    public async Task Update_without_skills_leaves_them_alone()
    {
        var handler = new UpdateSettingsHandler(_settings, new FakeCurrentUser(UserId));
        await handler.HandleAsync(new UpdateSettingsCommand("en", 75, false, ["formal"], ["concise"]), default);

        var again = await handler.HandleAsync(new UpdateSettingsCommand("nl", 60, true), default);

        Assert.Equal(["formal"], again.DocumentSkills);
        Assert.Equal(["concise"], again.InterviewSkills);
    }
}
