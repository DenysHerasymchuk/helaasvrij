using InternshipCoach.Application.Chat;

namespace InternshipCoach.Application.Tests.Chat;

public class ChatRoutesTests
{
    [Fact]
    public void Allowed_route_is_returned_and_line_stripped()
    {
        var (reply, nav) = ChatRoutes.ExtractNavigation("Add it on the Postings page.\nNAVIGATE: /postings");

        Assert.Equal("/postings", nav);
        Assert.Equal("Add it on the Postings page.", reply);
    }

    [Theory]
    [InlineData("NAVIGATE: /admin")]
    [InlineData("NAVIGATE: https://evil.example")]
    [InlineData("NAVIGATE: //evil.example/postings")]
    [InlineData("NAVIGATE: /postings/../admin")]
    [InlineData("NAVIGATE: javascript:alert(1)")]
    [InlineData("NAVIGATE:")]
    public void Unknown_or_malicious_targets_are_ignored_and_stripped(string line)
    {
        var (reply, nav) = ChatRoutes.ExtractNavigation($"Sure.\n{line}");

        Assert.Null(nav);
        Assert.Equal("Sure.", reply);
    }

    [Fact]
    public void Reply_without_hint_is_unchanged()
    {
        var (reply, nav) = ChatRoutes.ExtractNavigation("Hello there.");

        Assert.Null(nav);
        Assert.Equal("Hello there.", reply);
    }

    [Fact]
    public void Trailing_slash_and_case_of_keyword_are_tolerated()
    {
        var (_, nav) = ChatRoutes.ExtractNavigation("x\nnavigate: /gaps/");

        Assert.Equal("/gaps", nav);
    }
}
