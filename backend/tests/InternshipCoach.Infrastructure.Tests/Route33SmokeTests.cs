using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Infrastructure.Llm;

namespace InternshipCoach.Infrastructure.Tests;

/// <summary>Skipped unless R33_API_KEY is set. Sends one tiny request to the real gateway.</summary>
public sealed class RequiresRoute33KeyFactAttribute : FactAttribute
{
    public RequiresRoute33KeyFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("R33_API_KEY")))
        {
            Skip = "Set R33_API_KEY to run the real route33 smoke test.";
        }
    }
}

public class Route33SmokeTests
{
    [RequiresRoute33KeyFact]
    public async Task Tiny_json_request_goes_through_route33()
    {
        var options = new Route33Options { ApiKey = Environment.GetEnvironmentVariable("R33_API_KEY") };
        var transport = new Route33LlmTransport(options);

        var response = await transport.SendAsync(
            LlmModel.Smart,
            "Reply with only this JSON and nothing else: {\"ok\":true}",
            [new ChatTurn(ChatRole.User, "Go.")],
            4000,
            CancellationToken.None);

        Assert.Contains("ok", response.Text);
        Assert.True(response.OutputTokens > 0);
    }
}
