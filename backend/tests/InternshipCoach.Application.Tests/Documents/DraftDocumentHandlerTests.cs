using System.Text.Json;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Documents.Draft;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Documents;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Tests.Documents;

public class DraftDocumentHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    private const string Readme = "weather-app: built a REST API in C# and shipped it with Docker.";

    private static (DraftDocumentHandler Handler, FakeLlmTransport Transport, FakeDocumentRepository Docs, Posting Posting) Build(bool confirm, params string[] replies)
    {
        var source = Source.Create(UserId, SourceKind.GitHub, "octocat");
        var doc = source.AddDocument("weather-app/README.md", Readme);
        source.MarkReady(Clock.UtcNow);
        var profile = new FakeProfileRepository([source]);
        var item = ProfileItem.Create(UserId, ProfileItemKind.Skill, "Docker", doc.Id, "shipped it with Docker", new CreatedAt(Clock.UtcNow));
        item.SetConfirmed(confirm);
        profile.Items.Add(item);

        var posting = Posting.Create(UserId, "Acme", "Intern", "Gent", PostingLanguage.Nl, null, "Stage bij Acme in Gent.", null,
            [("Docker ervaring", Importance.Required)], false, new CreatedAt(Clock.UtcNow));
        var postings = new FakePostingRepository();
        postings.Items.Add(posting);

        var transport = new FakeLlmTransport(replies);
        var llm = new LlmClient(transport, new FakeLlmCallLog(), Clock, new LlmLimits());
        var docs = new FakeDocumentRepository();
        return (new DraftDocumentHandler(llm, postings, new GapEvidenceLoader(profile), docs, new FakeCurrentUser(UserId), Clock), transport, docs, posting);
    }

    private static string Reply(string text, params string[] quotes) =>
        JsonSerializer.Serialize(new { language = "nl", text, quotes });

    [Fact]
    public async Task Cover_letter_is_saved_in_the_posting_language_with_a_verified_quote()
    {
        var (handler, transport, docs, posting) = Build(true, Reply("Beste Acme,\n\nIk bouwde weather-app met Docker.", "shipped it with Docker"));

        var document = await handler.HandleAsync(new DraftDocumentCommand(posting.Id, DocumentKind.CoverLetter, null), default);

        Assert.NotNull(document);
        Assert.Equal(DocumentKind.CoverLetter, document.Kind);
        Assert.Equal("nl", document.Language);
        Assert.Equal("Cover letter - Acme", document.Title);
        Assert.Same(document, Assert.Single(docs.Documents));
        Assert.Contains("Dutch", transport.Requests[0].System);
        Assert.Contains("<untrusted_data name=\"evidence_quotes\">", transport.Requests[0].Turns[0].Content);
    }

    [Fact]
    public async Task Requested_language_overrides_the_posting_language()
    {
        var (handler, transport, _, posting) = Build(true, Reply("Hello Acme, I shipped weather-app with Docker.", "shipped it with Docker"));

        var document = await handler.HandleAsync(new DraftDocumentCommand(posting.Id, DocumentKind.CoverLetter, "en"), default);

        Assert.Equal("en", document!.Language);
        Assert.Contains("English", transport.Requests[0].System);
    }

    [Fact]
    public async Task Recruiter_message_over_300_characters_is_retried_once_then_saved()
    {
        var (handler, transport, _, posting) = Build(true,
            Reply(new string('a', 301), "shipped it with Docker"),
            Reply("Hoi, ik bouwde weather-app met Docker. Heeft u even tijd?", "shipped it with Docker"));

        var document = await handler.HandleAsync(new DraftDocumentCommand(posting.Id, DocumentKind.RecruiterMessage, null), default);

        Assert.True(document!.Content.Length <= 300);
        Assert.Equal(2, transport.Requests.Count);
        Assert.Contains("rejected", transport.Requests[1].Turns[0].Content);
    }

    [Fact]
    public async Task Rejects_a_draft_when_no_quote_is_verbatim_and_never_calls_a_third_time()
    {
        var (handler, transport, docs, posting) = Build(true,
            Reply("Ik ken Kubernetes.", "ran Kubernetes in production"),
            Reply("Ik ken Kubernetes.", "ran Kubernetes in production"));

        await Assert.ThrowsAsync<DraftRejectedException>(() =>
            handler.HandleAsync(new DraftDocumentCommand(posting.Id, DocumentKind.CoverLetter, null), default));

        Assert.Equal(2, transport.Requests.Count);
        Assert.Empty(docs.Documents);
    }

    [Fact]
    public async Task Rejects_numbers_that_appear_nowhere_in_the_evidence_or_posting()
    {
        var (handler, _, docs, posting) = Build(true,
            Reply("Ik heb 9 jaar ervaring met Docker.", "shipped it with Docker"),
            Reply("Ik heb 9 jaar ervaring met Docker.", "shipped it with Docker"));

        await Assert.ThrowsAsync<DraftRejectedException>(() =>
            handler.HandleAsync(new DraftDocumentCommand(posting.Id, DocumentKind.CoverLetter, null), default));
        Assert.Empty(docs.Documents);
    }

    [Fact]
    public async Task Needs_a_confirmed_profile_item_and_an_existing_posting()
    {
        var (handler, transport, _, posting) = Build(false);

        await Assert.ThrowsAsync<GapInputException>(() =>
            handler.HandleAsync(new DraftDocumentCommand(posting.Id, DocumentKind.CoverLetter, null), default));
        Assert.Null(await handler.HandleAsync(new DraftDocumentCommand(Guid.NewGuid(), DocumentKind.CoverLetter, null), default));
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public async Task A_cv_cannot_be_drafted_this_way()
    {
        var (handler, _, _, posting) = Build(true);
        await Assert.ThrowsAsync<DraftRejectedException>(() =>
            handler.HandleAsync(new DraftDocumentCommand(posting.Id, DocumentKind.Cv, null), default));
    }
}
