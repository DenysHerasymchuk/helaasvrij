using System.Net;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Infrastructure.Persistence;
using InternshipCoach.Infrastructure.Web;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Tests;

public class PostingInfrastructureTests
{
    private static readonly CreatedAt Now = new(new DateTimeOffset(2026, 11, 24, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task User_only_sees_own_postings_and_requirements()
    {
        var options = new DbContextOptionsBuilder<InternshipCoachDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await Add(options, a, "A-Corp");
        await Add(options, b, "B-Corp");

        await using var asA = new InternshipCoachDbContext(options, new FixedUser(a));
        var repo = new PostingRepository(asA);

        var list = await repo.ListAsync(null, null, default);
        Assert.Equal("A-Corp", Assert.Single(list).Company);
        Assert.Single(await repo.ListAsync(PostingLanguage.Nl, "a-corp", default));
        Assert.Null(await repo.FindAsync((await new PostingRepository(new InternshipCoachDbContext(options, new FixedUser(b))).ListAsync(null, null, default))[0].Id, default));
        Assert.Single(asA.Requirements.ToList());
    }

    [Fact]
    public async Task Deleting_a_posting_removes_its_requirements()
    {
        var options = new DbContextOptionsBuilder<InternshipCoachDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var a = Guid.NewGuid();
        await Add(options, a, "A-Corp");

        await using var db = new InternshipCoachDbContext(options, new FixedUser(a));
        var repo = new PostingRepository(db);
        await repo.DeleteAsync((await repo.ListAsync(null, null, default)).Single(), default);

        Assert.Empty(await db.Requirements.ToListAsync());
    }

    [Theory]
    [InlineData("http://127.0.0.1/x")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("https://www.linkedin.com/jobs/1")]
    public async Task Fetcher_refuses_unsafe_urls_before_sending_anything(string url)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        await Assert.ThrowsAsync<PageFetchException>(() => new SafePageFetcher(handler).FetchHtmlAsync(new Uri(url), default));
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task Fetcher_refuses_a_redirect_to_a_private_address()
    {
        var handler = new StubHandler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new Uri("http://169.254.169.254/latest/meta-data");
            return response;
        });

        await Assert.ThrowsAsync<PageFetchException>(() => new SafePageFetcher(handler).FetchHtmlAsync(new Uri("https://jobs.example.com/1"), default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Fetcher_sends_its_user_agent_and_returns_html()
    {
        var handler = new StubHandler(request =>
        {
            Assert.Contains("InternshipCoachBot", request.Headers.UserAgent.ToString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<p>hi</p>", System.Text.Encoding.UTF8, "text/html") };
        });

        Assert.Equal("<p>hi</p>", await new SafePageFetcher(handler).FetchHtmlAsync(new Uri("https://jobs.example.com/1"), default));
    }

    [Fact]
    public async Task Fetcher_refuses_big_and_non_html_responses()
    {
        var big = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('a', SafePageFetcher.MaxBytes + 10), System.Text.Encoding.UTF8, "text/html")
        });
        await Assert.ThrowsAsync<PageFetchException>(() => new SafePageFetcher(big).FetchHtmlAsync(new Uri("https://jobs.example.com/1"), default));

        var pdf = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) { Headers = { ContentType = new("application/pdf") } } });
        await Assert.ThrowsAsync<PageFetchException>(() => new SafePageFetcher(pdf).FetchHtmlAsync(new Uri("https://jobs.example.com/1"), default));
    }

    private static async Task Add(DbContextOptions<InternshipCoachDbContext> options, Guid user, string company)
    {
        await using var db = new InternshipCoachDbContext(options, new FixedUser(user));
        db.Postings.Add(Posting.Create(user, company, "Intern", "Gent", PostingLanguage.Nl, null, "text", null,
            [("C#", Importance.Required)], false, Now));
        await db.SaveChangesAsync();
    }

    private sealed class FixedUser(Guid id) : ICurrentUser
    {
        public Guid UserId { get; } = id;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }
}
