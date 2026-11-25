using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Applications;
using InternshipCoach.Application.Chat.Confirm;
using InternshipCoach.Application.Chat.Send;
using InternshipCoach.Application.Chat.Tools;
using InternshipCoach.Application.Gaps;
using InternshipCoach.Application.Journey;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Postings;
using InternshipCoach.Application.Postings.Add;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Profile;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Tests.Chat;

/// <summary>A signed-in student with fake repositories and a scripted LLM transport.</summary>
internal sealed class ChatFixture
{
    public static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);

    public FakeCurrentUser User { get; } = new();
    public FakePostingRepository Postings { get; } = new();
    public FakeProfileRepository Profile { get; } = new();
    public FakeSourceRepository Sources { get; } = new();
    public FakeApplicationRepository Applications { get; }
    public FakeLlmTransport Transport { get; }
    public FakeLlmCallLog Log { get; } = new();
    public ChatToolbox Tools { get; }
    public SendChatHandler Handler { get; }
    public ConfirmChatActionHandler Confirm { get; }

    public ChatFixture(params string[] replies)
    {
        Applications = new FakeApplicationRepository(Postings);
        Transport = new FakeLlmTransport(replies);
        var clock = new FakeClock(Now);
        var llm = new LlmClient(Transport, Log, clock, new LlmLimits());
        var importer = new PostingImporter(new PostingExtractor(llm), Postings, User, clock);
        Tools = new ChatToolbox(
            Profile, Sources, Postings, Applications, new FakeJobSnapshot(),
            new ListGapsHandler(Postings),
            new GetJourneyHandler(Sources, Profile, Postings, new FakeDocumentRepository(), Applications),
            new CreateApplicationHandler(Applications, Postings, new FakeSprintRepository(), User, clock),
            new UpdateApplicationHandler(Applications, clock),
            new AddPostingFromJobHandler(new FakeJobSnapshot(), importer, Postings));
        Handler = new SendChatHandler(User, llm, Tools);
        Confirm = new ConfirmChatActionHandler(Tools);
    }

    public Posting AddPosting(string company, string evidence = "q")
    {
        var posting = Posting.Create(
            User.UserId, company, "Software intern", "Kortrijk", PostingLanguage.En, null, "RAW POSTING TEXT", null,
            [("C#", Importance.Required), ("Docker", Importance.Required)], false, new CreatedAt(Now));
        posting.Requirements[0].RecordAnalysis(Coverage.Strong, evidence, Guid.NewGuid(), null, null, null, Now);
        posting.Requirements[1].RecordAnalysis(Coverage.None, null, null, "Add a Dockerfile to weather-app", "weather-app", null, Now);
        Postings.Items.Add(posting);
        return posting;
    }

    public JobApplication AddToBoard(Posting posting)
    {
        var application = JobApplication.Create(User.UserId, posting.Id, new CreatedAt(Now));
        Applications.Items.Add(application);
        return application;
    }

    public void AddGitHub(string repo)
    {
        var source = Source.Create(User.UserId, SourceKind.GitHub, "lotte-dev");
        source.AddDocument($"{repo}/README.md", "SECRET README TEXT", "{}");
        source.AddDocument($"{repo}/summary", "Repository summary", $$"""{"repo":"{{repo}}","language":"C#","commits":42,"hasDockerfile":false}""");
        source.MarkReady(Now);
        Sources.Items.Add(source);
    }

    public void AddConfirmedItem(ProfileItemKind kind, string name, string quote)
    {
        var item = ProfileItem.Create(User.UserId, kind, name, Guid.NewGuid(), quote, new CreatedAt(Now));
        item.SetConfirmed(true);
        Profile.Items.Add(item);
    }

    private sealed class FakeJobSnapshot : IJobSnapshot
    {
        public Task<IReadOnlyList<Job>?> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Job>?>([new Job("42", "Backend intern", "Barco", "Kortrijk", "C# and Azure", "2026-11-20T00:00:00Z", "https://www.adzuna.be/land/ad/42")]);
    }
}
