using System.Text.Json;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Documents.TailorCv;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Tests.Documents;

public class TailorCvHandlerTests
{
    private const string Cv = """
        Jan Peeters
        Built a weather app in React with a .NET API.
        Worked 2 years as a cashier at Colruyt.
        Skills: Docker, C#, Dutch (native)
        """;

    private const string Posting = "Stage .NET developer bij Acme in Gent. Docker is een plus.";

    private static readonly Guid UserId = Guid.NewGuid();

    private static (TailorCvHandler Handler, FakeLlmTransport Transport, FakeLlmCallLog Log, FakeDocumentRepository Repo) Create(params string[] replies)
    {
        var transport = new FakeLlmTransport(replies);
        var log = new FakeLlmCallLog();
        var clock = new FakeClock(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));
        var llm = new LlmClient(transport, log, clock, new LlmLimits());
        var repo = new FakeDocumentRepository();
        return (new TailorCvHandler(llm, repo, Postings, new FakeCurrentUser(UserId), clock), transport, log, repo);
    }

    // The real repository filters by user (PostingInfrastructureTests); here only the student's own posting is in the fake.
    private static readonly Domain.Postings.Posting SavedPosting = Domain.Postings.Posting.Create(UserId, "Acme", "Stage .NET", "Gent", PostingLanguage.Nl, null,
        "Saved posting: .NET stage in Gent, Docker required.", null, [("Docker", Importance.Required)], false, new CreatedAt(DateTimeOffset.UtcNow));

    private static readonly FakePostingRepository Postings = new() { Items = { SavedPosting } };

    [Fact]
    public async Task Saved_posting_text_is_sent_instead_of_the_pasted_text()
    {
        var (handler, transport, _, _) = Create(Reply());

        var result = await handler.HandleAsync(new TailorCvCommand(Cv, "pasted text that must be ignored", null, SavedPosting.Id), CancellationToken.None);

        Assert.True(result.Succeeded);
        var prompt = transport.Requests.Single().Turns[0].Content;
        Assert.Contains("Saved posting: .NET stage in Gent", prompt);
        Assert.DoesNotContain("pasted text that must be ignored", prompt);
    }

    [Fact]
    public async Task Unknown_or_other_users_posting_id_is_not_found_without_calling_the_model()
    {
        var (handler, transport, _, repo) = Create(Reply());

        var result = await handler.HandleAsync(new TailorCvCommand(Cv, null, null, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.PostingNotFound);
        Assert.False(result.Succeeded);
        Assert.Empty(transport.Requests);
        Assert.Empty(repo.Documents);
    }

    private static string Reply(string language = "nl", object[]? bullets = null, object[]? skills = null, string summary = "Student met .NET-ervaring.") =>
        JsonSerializer.Serialize(new
        {
            language,
            title = "Jan Peeters - .NET stage",
            summary,
            sections = new object[]
            {
                new { heading = "Projecten", bullets = bullets ?? [new { text = "Weather app in React met een .NET API", quote = "Built a weather app in React with a .NET API." }] },
                new { heading = "Leeg", bullets = new[] { new { text = "Leidde een team", quote = "Led a team of five." } } }
            },
            skills = skills ?? [new { name = "Docker", quote = "Docker" }]
        });

    [Fact]
    public async Task Bullets_and_skills_with_unverified_quotes_are_dropped_and_counted()
    {
        var (handler, _, _, repo) = Create(Reply(skills: [new { name = "Docker", quote = "Docker" }, new { name = "Kubernetes", quote = "Kubernetes" }]));

        var result = await handler.HandleAsync(new TailorCvCommand(Cv, Posting, null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.DroppedCount);
        var content = result.Document!.Content;
        Assert.Contains("- Weather app in React met een .NET API", content);
        Assert.Contains("Docker", content);
        Assert.DoesNotContain("Kubernetes", content);
        Assert.DoesNotContain("Leidde een team", content);
        Assert.DoesNotContain("## Leeg", content); // empty section removed
        Assert.StartsWith("# Jan Peeters - .NET stage", content);
        Assert.Same(result.Document, Assert.Single(repo.Documents));
        Assert.Equal(UserId, result.Document.UserId);
    }

    [Fact]
    public async Task Numbers_not_in_the_cv_are_dropped_even_with_a_valid_quote()
    {
        var bullets = new object[]
        {
            new { text = "Werkte 2 jaar als kassier bij Colruyt", quote = "Worked 2 years as a cashier at Colruyt." },
            new { text = "Werkte 5 jaar als kassier bij Colruyt", quote = "Worked 2 years as a cashier at Colruyt." }
        };
        var (handler, _, _, _) = Create(Reply(bullets: bullets, summary: "Student met 10 jaar ervaring."));

        var result = await handler.HandleAsync(new TailorCvCommand(Cv, Posting, null), CancellationToken.None);

        Assert.Contains("Werkte 2 jaar", result.Document!.Content);
        Assert.DoesNotContain("5 jaar", result.Document.Content);
        Assert.DoesNotContain("10 jaar", result.Document.Content);
        Assert.Equal(3, result.DroppedCount); // inflated bullet, invented "Leidde een team", summary
    }

    [Fact]
    public async Task Language_from_the_model_passes_through_when_none_is_chosen()
    {
        var (handler, _, _, _) = Create(Reply(language: "fr"));

        var result = await handler.HandleAsync(new TailorCvCommand(Cv, Posting, null), CancellationToken.None);

        Assert.Equal("fr", result.Document!.Language);
        Assert.Contains("## Compétences", result.Document.Content);
    }

    [Fact]
    public async Task Chosen_language_is_sent_to_the_model_and_kept()
    {
        var (handler, transport, _, _) = Create(Reply(language: "nl"));

        var result = await handler.HandleAsync(new TailorCvCommand(Cv, Posting, "en"), CancellationToken.None);

        Assert.Equal("en", result.Document!.Language);
        Assert.Contains("English", transport.Requests[0].Turns[0].Content);
    }

    [Fact]
    public async Task Uses_the_smart_model_with_wrapped_inputs_and_logs_the_feature()
    {
        var (handler, transport, log, _) = Create(Reply());

        await handler.HandleAsync(new TailorCvCommand(Cv, Posting, null), CancellationToken.None);

        var request = Assert.Single(transport.Requests);
        Assert.Equal(LlmModel.Smart, request.Model);
        Assert.True(request.MaxTokens >= LlmModels.SmartMinMaxTokens);
        Assert.Contains("<untrusted_data name=\"cv\">", request.Turns[0].Content);
        Assert.Contains("<untrusted_data name=\"job_posting\">", request.Turns[0].Content);
        Assert.Equal(TailorCvHandler.Feature, Assert.Single(log.Records).Feature);
    }

    [Theory]
    [InlineData("", Posting, null, "cvText")]
    [InlineData("   ", Posting, null, "cvText")]
    [InlineData(Cv, "", null, "postingText")]
    [InlineData(Cv, Posting, "de", "language")]
    public async Task Invalid_input_is_rejected_without_calling_the_model(string cv, string posting, string? language, string field)
    {
        var (handler, transport, _, repo) = Create();

        var result = await handler.HandleAsync(new TailorCvCommand(cv, posting, language), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.True(result.Errors!.ContainsKey(field));
        Assert.Empty(transport.Requests);
        Assert.Empty(repo.Documents);
    }

    [Fact]
    public async Task Cv_over_20000_chars_is_rejected()
    {
        var (handler, transport, _, _) = Create();

        var result = await handler.HandleAsync(new TailorCvCommand(new string('a', 20_001), Posting, null), CancellationToken.None);

        Assert.True(result.Errors!.ContainsKey("cvText"));
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task Nothing_verified_means_no_document_is_saved()
    {
        var (handler, _, _, repo) = Create(Reply(bullets: [new { text = "x", quote = "not in cv" }], skills: []));

        var result = await handler.HandleAsync(new TailorCvCommand(Cv, Posting, null), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(repo.Documents);
    }
}
