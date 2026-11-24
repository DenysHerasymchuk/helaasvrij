using InternshipCoach.Application.Llm;

namespace InternshipCoach.Application.Tests.Llm;

public class QuoteCheckTests
{
    private const string Source = "Built a REST API in ASP.NET Core.\nDeployed it with Docker on a home server.\n\nWrote unit tests with xUnit.";

    [Fact]
    public void Exact_substring_is_verbatim() =>
        Assert.True(QuoteCheck.IsVerbatim("Deployed it with Docker", Source));

    [Fact]
    public void Whitespace_differences_are_ignored() =>
        Assert.True(QuoteCheck.IsVerbatim("ASP.NET Core.   Deployed it\twith Docker", Source));

    [Fact]
    public void Spliced_quote_of_two_real_fragments_is_rejected() =>
        Assert.False(QuoteCheck.IsVerbatim("Built a REST API with xUnit", Source));

    [Fact]
    public void Changed_word_is_rejected() =>
        Assert.False(QuoteCheck.IsVerbatim("Deployed it with Kubernetes", Source));

    [Theory]
    [InlineData("", "source")]
    [InlineData("   ", "source")]
    [InlineData(null, "source")]
    [InlineData("quote", null)]
    public void Empty_input_is_rejected(string? quote, string? source) =>
        Assert.False(QuoteCheck.IsVerbatim(quote, source));
}
