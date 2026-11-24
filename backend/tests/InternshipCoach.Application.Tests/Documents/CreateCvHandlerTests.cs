using System.Text.Json;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Documents.CreateCv;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Settings;
using InternshipCoach.Domain.Users;

namespace InternshipCoach.Application.Tests.Documents;

public class CreateCvHandlerTests
{
    private static readonly CreatedAt Now = new(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));

    private readonly User _user = User.CreateWithPassword(Email.Create("jan@example.com"), "Jan Peeters", "hash", Now);
    private readonly FakeProfileRepository _profile = new();
    private readonly FakeSettingsRepository _settings = new();
    private readonly FakeDocumentRepository _documents = new();
    private FakeLlmTransport _transport = new();
    private FakeLlmCallLog _log = new();

    private ProfileItem AddItem(ProfileItemKind kind, string name, string quote, string? level = null, bool confirmed = true, Guid? owner = null)
    {
        var item = ProfileItem.Create(owner ?? _user.Id, kind, name, level, Guid.NewGuid(), quote, Now);
        item.SetConfirmed(confirmed);
        _profile.Items.Add(item);
        return item;
    }

    private Task<CreateCvResult> Run(string? language, params string[] replies)
    {
        _transport = new FakeLlmTransport(replies);
        _log = new FakeLlmCallLog();
        var clock = new FakeClock(Now.Value);
        var users = new FakeUserRepository();
        users.Users.Add(_user);
        var handler = new CreateCvHandler(
            new LlmClient(_transport, _log, clock, new LlmLimits()), _documents, _profile, _settings, users, new FakeCurrentUser(_user.Id), clock);
        return handler.HandleAsync(new CreateCvCommand(language), CancellationToken.None);
    }

    private static string Reply(string summary, params (object ItemId, string Text)[] bullets) =>
        JsonSerializer.Serialize(new { summary, bullets = bullets.Select(b => new { itemId = b.ItemId.ToString(), text = b.Text }) });

    [Fact]
    public async Task No_confirmed_items_returns_an_error_without_calling_the_model()
    {
        AddItem(ProfileItemKind.Skill, "C#", "C#", confirmed: false);
        AddItem(ProfileItemKind.Skill, "Go", "Go", owner: Guid.NewGuid());

        var result = await Run(null);

        Assert.False(result.Succeeded);
        Assert.Equal(CreateCvResult.NoConfirmedItems, result.Error);
        Assert.Empty(_transport.Requests);
        Assert.Empty(_documents.Documents);
    }

    [Fact]
    public async Task Bullets_with_unknown_unconfirmed_or_other_users_item_ids_are_dropped_and_counted()
    {
        var skill = AddItem(ProfileItemKind.Skill, "Docker", "Docker");
        var project = AddItem(ProfileItemKind.Project, "weather-app", "Built a weather app in React with a .NET API.");
        var unconfirmed = AddItem(ProfileItemKind.Skill, "Kubernetes", "Kubernetes", confirmed: false);
        var foreign = AddItem(ProfileItemKind.Experience, "CTO at Acme", "CTO at Acme", owner: Guid.NewGuid());

        var result = await Run("en", Reply("Student who builds web apps.",
            (skill.Id, "Docker"),
            (project.Id, "Weather app in React with a .NET API"),
            (unconfirmed.Id, "Kubernetes"),
            (foreign.Id, "CTO at Acme"),
            (Guid.NewGuid(), "Led a team"),
            ("not-a-guid", "Speaks Japanese")));

        Assert.True(result.Succeeded);
        Assert.Equal(4, result.DroppedCount);
        var content = result.Document!.Content;
        Assert.StartsWith("# Jan Peeters\n\nStudent who builds web apps.\n", content);
        Assert.Contains("## Skills\n\nDocker\n", content);
        Assert.Contains("## Projects\n\n- Weather app in React with a .NET API\n", content);
        Assert.DoesNotContain("Kubernetes", content);
        Assert.DoesNotContain("CTO", content);
        Assert.DoesNotContain("Led a team", content);
        Assert.DoesNotContain("Japanese", content);
        Assert.DoesNotContain("## Experience", content);
        Assert.Equal("CV (English)", result.Document.Title);
        Assert.Equal(_user.Id, result.Document.UserId);
        Assert.Same(result.Document, Assert.Single(_documents.Documents));

        // Only confirmed items of this user reach the model.
        var prompt = _transport.Requests.Single().Turns[0].Content;
        Assert.Contains(skill.Id.ToString(), prompt);
        Assert.DoesNotContain(unconfirmed.Id.ToString(), prompt);
        Assert.DoesNotContain(foreign.Id.ToString(), prompt);
    }

    [Fact]
    public async Task Numbers_not_in_the_items_own_text_are_dropped()
    {
        var job = AddItem(ProfileItemKind.Experience, "Cashier at Colruyt", "Worked 2 years as a cashier at Colruyt.");
        var dutch = AddItem(ProfileItemKind.Language, "Dutch", "Dutch (native)", level: "C2");
        AddItem(ProfileItemKind.Project, "api", "Built an API serving 500 users.");

        var result = await Run("en", Reply("Student with 10 years of experience.",
            (job.Id, "Cashier at Colruyt for 2 years"),
            (job.Id, "Cashier at Colruyt for 5 years"),
            (job.Id, "Served 500 customers a day"), // 500 belongs to another item
            (dutch.Id, "Dutch")));

        var content = result.Document!.Content;
        Assert.Contains("- Cashier at Colruyt for 2 years", content);
        Assert.DoesNotContain("5 years", content);
        Assert.DoesNotContain("500", content);
        Assert.DoesNotContain("10 years", content);
        Assert.Contains("## Languages\n\n- Dutch (C2)\n", content);
        Assert.Equal(3, result.DroppedCount);
    }

    [Fact]
    public async Task Language_defaults_to_the_users_setting()
    {
        var skill = AddItem(ProfileItemKind.Skill, "Docker", "Docker");
        _settings.Rows.Add(UserSettings.Create(_user.Id, "nl", 75, false));

        var result = await Run(null, Reply("Student.", (skill.Id, "Docker")));

        Assert.Equal("nl", result.Document!.Language);
        Assert.Equal("CV (Nederlands)", result.Document.Title);
        Assert.Contains("## Vaardigheden", result.Document.Content);
        Assert.Contains("Dutch", _transport.Requests[0].Turns[0].Content);
    }

    [Fact]
    public async Task Language_falls_back_to_english_without_a_setting()
    {
        var skill = AddItem(ProfileItemKind.Skill, "Docker", "Docker");

        var result = await Run(null, Reply("Student.", (skill.Id, "Docker")));

        Assert.Equal("en", result.Document!.Language);
    }

    [Fact]
    public async Task Unknown_language_is_rejected_without_calling_the_model()
    {
        AddItem(ProfileItemKind.Skill, "Docker", "Docker");

        var result = await Run("de");

        Assert.False(result.Succeeded);
        Assert.Empty(_transport.Requests);
    }

    [Fact]
    public async Task Uses_the_smart_model_with_wrapped_items_and_logs_the_feature()
    {
        var skill = AddItem(ProfileItemKind.Skill, "Docker", "Docker");

        await Run("fr", Reply("Étudiant.", (skill.Id, "Docker")));

        var request = Assert.Single(_transport.Requests);
        Assert.Equal(LlmModel.Smart, request.Model);
        Assert.True(request.MaxTokens >= LlmModels.SmartMinMaxTokens);
        Assert.Contains("<untrusted_data name=\"profile_items\">", request.Turns[0].Content);
        Assert.Equal(CreateCvHandler.Feature, Assert.Single(_log.Records).Feature);
        Assert.Equal("cv-create", CreateCvHandler.Feature);
    }

    [Fact]
    public async Task Nothing_matched_means_no_document_is_saved()
    {
        AddItem(ProfileItemKind.Skill, "Docker", "Docker");

        var result = await Run("en", Reply("Student.", (Guid.NewGuid(), "Kubernetes")));

        Assert.False(result.Succeeded);
        Assert.Empty(_documents.Documents);
    }
}
