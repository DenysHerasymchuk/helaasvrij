using System.Net;
using InternshipCoach.Application.Sources;
using InternshipCoach.Application.Sources.Cv;
using InternshipCoach.Application.Sources.GitHub;
using InternshipCoach.Application.Sources.Site;
using InternshipCoach.Application.Sources.Text;
using InternshipCoach.Application.Tests.TestDoubles;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Tests.Sources;

public class SourceHandlerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly FakeClock Clock = new(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
    private static readonly FakeCurrentUser Me = new(UserId);

    private static GitHubRepoData Repo(string name, string? readme = "# Hello") => new(
        name, $"https://github.com/x/{name}", "A demo", ["demo"], "C#", new Dictionary<string, long> { ["C#"] = 90, ["HTML"] = 10 },
        readme, ["src/", "Dockerfile"], true, false, true, 42, null);

    private static byte[] Pdf() => "%PDF-1.4 fake"u8.ToArray();

    [Fact]
    public async Task GitHub_sync_stores_readme_and_summary_per_repo()
    {
        var repo = new FakeSourceRepository();
        var client = new FakeGitHubClient { Repos = [Repo("weather-app"), Repo("no-readme", null)] };
        var handler = new SyncGitHubHandler(repo, client, new FakeUserLookup(null), Me, Clock);

        var result = await handler.HandleAsync(new SyncGitHubCommand(" octocat "), CancellationToken.None);

        Assert.Equal("octocat", client.LastUsername);
        Assert.Equal(SourceStatus.Ready, result.Status);
        Assert.Equal(3, result.DocumentCount);
        var paths = repo.Items.Single().Documents.Select(d => d.Path).ToList();
        Assert.Contains("weather-app/README.md", paths);
        Assert.Contains("weather-app/summary", paths);
        Assert.DoesNotContain("no-readme/README.md", paths);
        Assert.Contains("Commits: 42", repo.Items.Single().Documents.First(d => d.Path == "weather-app/summary").Text);
    }

    [Fact]
    public async Task GitHub_resync_replaces_the_old_source()
    {
        var repo = new FakeSourceRepository();
        var client = new FakeGitHubClient { Repos = [Repo("a")] };
        var handler = new SyncGitHubHandler(repo, client, new FakeUserLookup(null), Me, Clock);
        await handler.HandleAsync(new SyncGitHubCommand("octocat"), CancellationToken.None);
        await handler.HandleAsync(new SyncGitHubCommand("octocat"), CancellationToken.None);

        Assert.Single(repo.Items);
    }

    [Fact]
    public async Task GitHub_failure_is_recorded_and_keeps_the_previous_data()
    {
        var repo = new FakeSourceRepository();
        var client = new FakeGitHubClient { Repos = [Repo("a")] };
        var handler = new SyncGitHubHandler(repo, client, new FakeUserLookup(null), Me, Clock);
        await handler.HandleAsync(new SyncGitHubCommand("octocat"), CancellationToken.None);

        client.FailWith = "rate limited";
        var result = await handler.HandleAsync(new SyncGitHubCommand("octocat"), CancellationToken.None);

        Assert.Equal(SourceStatus.Failed, result.Status);
        Assert.Equal("rate limited", result.Error);
        Assert.Equal(2, result.DocumentCount);
    }

    [Theory]
    [InlineData("-bad")]
    [InlineData("has space")]
    [InlineData("a/b")]
    public async Task GitHub_rejects_invalid_usernames(string username)
    {
        var handler = new SyncGitHubHandler(new FakeSourceRepository(), new FakeGitHubClient(), new FakeUserLookup(null), Me, Clock);
        await Assert.ThrowsAsync<SourceInputException>(() => handler.HandleAsync(new SyncGitHubCommand(username), CancellationToken.None));
    }

    [Fact]
    public async Task GitHub_without_username_and_without_linked_login_asks_for_one()
    {
        var handler = new SyncGitHubHandler(new FakeSourceRepository(), new FakeGitHubClient(), new FakeUserLookup(null), Me, Clock);
        await Assert.ThrowsAsync<SourceInputException>(() => handler.HandleAsync(new SyncGitHubCommand(null), CancellationToken.None));
    }

    [Fact]
    public async Task Cv_upload_makes_one_document_per_page_and_skips_blank_pages()
    {
        var repo = new FakeSourceRepository();
        var handler = new UploadCvHandler(repo, new FakePdfTextExtractor("Page one", "  ", "Page three"), Me, Clock);

        var result = await handler.HandleAsync(new UploadCvCommand("cv.pdf", Pdf()), CancellationToken.None);

        Assert.Equal(2, result.DocumentCount);
        Assert.Equal(["cv.pdf#page1", "cv.pdf#page3"], repo.Items.Single().Documents.Select(d => d.Path));
    }

    [Fact]
    public async Task Cv_upload_rejects_non_pdf_oversize_and_textless_files()
    {
        var handler = new UploadCvHandler(new FakeSourceRepository(), new FakePdfTextExtractor(" "), Me, Clock);

        await Assert.ThrowsAsync<SourceInputException>(() => handler.HandleAsync(new UploadCvCommand("cv.docx", Pdf()), CancellationToken.None));
        await Assert.ThrowsAsync<SourceInputException>(() => handler.HandleAsync(new UploadCvCommand("cv.pdf", "not a pdf"u8.ToArray()), CancellationToken.None));
        await Assert.ThrowsAsync<SourceInputException>(() => handler.HandleAsync(new UploadCvCommand("cv.pdf", new byte[UploadCvHandler.MaxBytes + 1]), CancellationToken.None));
        await Assert.ThrowsAsync<SourceInputException>(() => handler.HandleAsync(new UploadCvCommand("cv.pdf", Pdf()), CancellationToken.None));
    }

    [Fact]
    public async Task Pasted_text_is_stored_and_capped()
    {
        var repo = new FakeSourceRepository();
        var handler = new AddTextHandler(repo, Me, Clock);

        var result = await handler.HandleAsync(new AddTextCommand("My CV", "Worked on things"), CancellationToken.None);
        Assert.Equal("My CV", result.Label);
        Assert.Equal(SourceKind.PastedText, result.Kind);

        await Assert.ThrowsAsync<SourceInputException>(() => handler.HandleAsync(new AddTextCommand("x", new string('a', 20_001)), CancellationToken.None));
        await Assert.ThrowsAsync<SourceInputException>(() => handler.HandleAsync(new AddTextCommand("x", "  "), CancellationToken.None));
    }

    [Fact]
    public async Task Site_is_fetched_and_a_fetch_failure_is_stored_as_failed()
    {
        var repo = new FakeSourceRepository();
        var ok = await new AddSiteHandler(repo, new FakeWebPageFetcher("Hi, I am Lotte"), Me, Clock)
            .HandleAsync(new AddSiteCommand("https://lotte.dev/about"), CancellationToken.None);
        Assert.Equal(SourceStatus.Ready, ok.Status);

        var failed = await new AddSiteHandler(repo, new FakeWebPageFetcher(null, "too slow"), Me, Clock)
            .HandleAsync(new AddSiteCommand("https://lotte.dev"), CancellationToken.None);
        Assert.Equal(SourceStatus.Failed, failed.Status);
        Assert.Equal("too slow", failed.Error);
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://10.0.0.1/")]
    [InlineData("http://localhost:5080/api")]
    [InlineData("http://192.168.1.5")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]/")]
    [InlineData("ftp://example.com/file")]
    [InlineData("https://www.linkedin.com/in/someone")]
    [InlineData("https://student.be/jobs")]
    [InlineData("https://app.jobteaser.com/x")]
    [InlineData("not a url")]
    public void Site_policy_rejects_private_blocked_and_non_http_addresses(string url) =>
        Assert.Throws<SourceInputException>(() => SiteUrlPolicy.Validate(url));

    [Fact]
    public void Site_policy_accepts_a_public_address() =>
        Assert.Equal("lotte.dev", SiteUrlPolicy.Validate("https://lotte.dev/about").Host);

    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.32.0.1", true)]
    [InlineData("::ffff:10.0.0.1", false)]
    [InlineData("fd00::1", false)]
    public void Ip_check_matches_private_ranges(string ip, bool isPublic) =>
        Assert.Equal(isPublic, SiteUrlPolicy.IsPublic(IPAddress.Parse(ip)));
}
