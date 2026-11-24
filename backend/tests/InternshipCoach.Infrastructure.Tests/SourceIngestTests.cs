using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Sources;
using InternshipCoach.Domain.Sources;
using InternshipCoach.Infrastructure.Persistence;
using InternshipCoach.Infrastructure.Sources;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace InternshipCoach.Infrastructure.Tests;

public class SourceIngestTests
{
    private sealed class FakeCurrentUser(Guid userId) : ICurrentUser
    {
        public Guid UserId { get; } = userId;
    }

    private static byte[] BuildPdf(params string[] pages)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var text in pages)
        {
            builder.AddPage(595, 842).AddText(text, 12, new UglyToad.PdfPig.Core.PdfPoint(50, 700), font);
        }

        return builder.Build();
    }

    [Fact]
    public void Pdf_text_is_extracted_per_page()
    {
        var pages = new PdfPigTextExtractor().ExtractPages(BuildPdf("Lotte Peeters Java developer", "Skills Docker Postgres"));

        Assert.Equal(2, pages.Count);
        Assert.Contains("Java developer", pages[0]);
        Assert.Contains("Docker", pages[1]);
    }

    [Fact]
    public void Broken_pdf_is_an_input_error() =>
        Assert.Throws<SourceInputException>(() => new PdfPigTextExtractor().ExtractPages("%PDF-1.4 garbage"u8.ToArray()));

    private const string ReposJson = """
        [
          {"name":"weather-app","html_url":"https://github.com/lotte/weather-app","description":"Forecasts","topics":["react","api"],
           "language":"TypeScript","fork":false,"default_branch":"main","pushed_at":"2026-11-01T10:00:00Z"},
          {"name":"forked","html_url":"https://github.com/lotte/forked","description":null,"topics":[],
           "language":null,"fork":true,"default_branch":"master","pushed_at":null}
        ]
        """;

    [Fact]
    public void Repo_list_is_mapped()
    {
        var repos = GitHubJson.ParseRepos(ReposJson);

        Assert.Equal(2, repos.Count);
        var first = repos[0];
        Assert.Equal("weather-app", first.Name);
        Assert.Equal(["react", "api"], first.Topics);
        Assert.Equal("TypeScript", first.Language);
        Assert.False(first.Fork);
        Assert.NotNull(first.PushedAt);
        Assert.True(repos[1].Fork);
        Assert.Null(repos[1].Description);
    }

    [Fact]
    public void Languages_are_mapped() =>
        Assert.Equal(1200, GitHubJson.ParseLanguages("""{"TypeScript":1200,"CSS":30}""")["TypeScript"]);

    [Fact]
    public void Tree_flags_dockerfile_tests_and_ci()
    {
        var info = GitHubJson.ParseTree("""
            {"tree":[
              {"path":"Dockerfile","type":"blob"},
              {"path":"src","type":"tree"},
              {"path":"src/app.ts","type":"blob"},
              {"path":"src/deep/file.ts","type":"blob"},
              {"path":"tests","type":"tree"},
              {"path":".github","type":"tree"},
              {"path":".github/workflows","type":"tree"},
              {"path":".github/workflows/ci.yml","type":"blob"}
            ]}
            """);

        Assert.True(info.HasDockerfile);
        Assert.True(info.HasTests);
        Assert.True(info.HasCi);
        Assert.Contains("src/", info.TopLevelPaths);
        Assert.DoesNotContain("src/deep/file.ts", info.TopLevelPaths);
    }

    [Fact]
    public void Tree_without_markers_has_no_flags()
    {
        var info = GitHubJson.ParseTree("""{"tree":[{"path":"README.md","type":"blob"},{"path":"main.py","type":"blob"}]}""");
        Assert.False(info.HasDockerfile || info.HasTests || info.HasCi);
    }

    [Fact]
    public void Commit_count_comes_from_the_last_page_link()
    {
        var link = """<https://api.github.com/repositories/1/commits?per_page=1&page=2>; rel="next", <https://api.github.com/repositories/1/commits?per_page=1&page=137>; rel="last" """;
        Assert.Equal(137, GitHubJson.ParseCommitCount(link, """[{"sha":"a"}]"""));
        Assert.Equal(1, GitHubJson.ParseCommitCount(null, """[{"sha":"a"}]"""));
        Assert.Equal(0, GitHubJson.ParseCommitCount(null, "[]"));
    }

    [Fact]
    public void Html_is_reduced_to_visible_text()
    {
        var text = WebPageFetcher.HtmlToText("<html><head><title>x</title></head><body><script>evil()</script><h1>Lotte</h1><p>Java   dev</p><style>a{}</style></body></html>");

        Assert.DoesNotContain("evil", text);
        Assert.DoesNotContain("a{}", text);
        Assert.Equal("Lotte\nJava dev", text);
    }

    [Fact]
    public async Task Sources_are_filtered_per_user_and_documents_are_deleted_with_the_source()
    {
        var options = new DbContextOptionsBuilder<InternshipCoachDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        async Task<Guid> Add(Guid user)
        {
            await using var db = new InternshipCoachDbContext(options, new FakeCurrentUser(user));
            var source = Source.Create(user, SourceKind.PastedText, "cv");
            source.AddDocument("pasted-text", "hello");
            source.MarkReady(DateTimeOffset.UtcNow);
            await new SourceRepository(db).AddAsync(source, CancellationToken.None);
            return source.Id;
        }

        var idA = await Add(userA);
        var idB = await Add(userB);

        await using var asA = new InternshipCoachDbContext(options, new FakeCurrentUser(userA));
        var repo = new SourceRepository(asA);
        Assert.Equal(idA, Assert.Single(await repo.ListAsync(CancellationToken.None)).Id);
        Assert.Null(await repo.FindAsync(idB, CancellationToken.None));

        await repo.RemoveAsync((await repo.FindAsync(idA, CancellationToken.None))!, CancellationToken.None);
        Assert.Empty(await repo.ListAsync(CancellationToken.None));
        // Only the other user's document is left.
        Assert.Equal(idB, Assert.Single(await asA.SourceDocuments.ToListAsync()).SourceId);
    }
}
