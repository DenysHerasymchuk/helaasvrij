using System.Net;
using InternshipCoach.Application.Postings.Fetching;

namespace InternshipCoach.Application.Tests.Postings;

public class FetchingTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:10.0.0.1")]
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("2002:7f00:1::")]
    public void Private_addresses_are_not_public(string ip) => Assert.False(PublicAddress.IsPublic(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("2606:4700:4700::1111")]
    public void Public_addresses_are_public(string ip) => Assert.True(PublicAddress.IsPublic(IPAddress.Parse(ip)));

    [Theory]
    [InlineData("https://www.linkedin.com/jobs/view/1")]
    [InlineData("https://linkedin.com/jobs/view/1")]
    [InlineData("https://nl.linkedin.com/x")]
    [InlineData("https://www.student.be/jobs/1")]
    [InlineData("https://app.jobteaser.com/en/job/1")]
    [InlineData("http://localhost/x")]
    [InlineData("http://127.0.0.1/x")]
    [InlineData("http://[::1]/x")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://intranet/x")]
    [InlineData("http://printer.local/x")]
    [InlineData("https://example.com:8443/x")]
    [InlineData("https://user:pw@example.com/x")]
    [InlineData("ftp://example.com/x")]
    [InlineData("not a url")]
    [InlineData("")]
    public void Unsafe_or_blocked_urls_are_rejected(string url)
    {
        var (uri, error) = PostingUrlPolicy.Check(url);
        Assert.Null(uri);
        Assert.False(string.IsNullOrEmpty(error));
    }

    [Fact]
    public void Public_https_url_is_accepted()
    {
        var (uri, error) = PostingUrlPolicy.Check("https://jobs.example.com/stage/1");
        Assert.NotNull(uri);
        Assert.Null(error);
    }

    [Fact]
    public void Json_ld_job_posting_is_read_from_a_graph()
    {
        const string html = """
            <html><head><script type="application/ld+json">
            {"@context":"https://schema.org","@graph":[{"@type":"WebSite"},
              {"@type":"JobPosting","title":"Stage dev","hiringOrganization":{"@type":"Organization","name":"Acme"},
               "jobLocation":{"@type":"Place","address":{"addressLocality":"Gent"}},
               "validThrough":"2026-12-31T00:00","description":"&lt;p&gt;Je kent C#.&lt;/p&gt;&lt;ul&gt;&lt;li&gt;Docker&lt;/li&gt;&lt;/ul&gt;"}]}
            </script></head><body></body></html>
            """;

        var data = JobPostingJsonLd.TryRead(html);

        Assert.NotNull(data);
        Assert.Equal("Stage dev", data.Title);
        Assert.Equal("Acme", data.Company);
        Assert.Equal("Gent", data.City);
        Assert.Equal(new DateOnly(2026, 12, 31), data.Deadline);
        Assert.Contains("Je kent C#.", data.Description);
        Assert.DoesNotContain("<", data.Description);
    }

    [Fact]
    public void Page_without_job_posting_json_ld_returns_null()
    {
        Assert.Null(JobPostingJsonLd.TryRead("<script type=\"application/ld+json\">{\"@type\":\"Organization\"}</script>"));
        Assert.Null(JobPostingJsonLd.TryRead("<script type=\"application/ld+json\">{broken</script>"));
    }

    [Fact]
    public void Html_is_reduced_to_text_without_scripts()
    {
        var text = HtmlText.ToText("<html><head><title>x</title></head><body><script>evil()</script><h1>Stage</h1><p>Kent u &amp; kunt u <b>Git</b>?</p></body></html>");

        Assert.Contains("Stage", text);
        Assert.Contains("Kent u & kunt u Git ?", text);
        Assert.DoesNotContain("evil", text);
        Assert.DoesNotContain("<", text);
    }
}
