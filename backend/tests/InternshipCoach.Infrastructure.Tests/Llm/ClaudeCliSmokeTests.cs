using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Infrastructure.Llm;

namespace InternshipCoach.Infrastructure.Tests.Llm;

/// <summary>Skipped unless CLAUDE_CLI_SMOKE=1. Runs the real local `claude -p` once (uses the user's subscription).</summary>
public sealed class RequiresClaudeCliFactAttribute : FactAttribute
{
    public RequiresClaudeCliFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("CLAUDE_CLI_SMOKE") != "1")
        {
            Skip = "Set CLAUDE_CLI_SMOKE=1 to run the real Claude CLI smoke test.";
        }
    }
}

public class ClaudeCliSmokeTests
{
    [RequiresClaudeCliFact]
    public async Task Local_cli_replies_ok()
    {
        var transport = new ClaudeCliLlmTransport(new ClaudeCliOptions { Mode = ClaudeCliMode.Local });

        var response = await transport.SendAsync(
            LlmModel.Cheap, "You are terse.", [new ChatTurn(ChatRole.User, "Reply with OK")], 100, CancellationToken.None);

        Assert.Contains("OK", response.Text);
        Assert.True(response.OutputTokens > 0);
    }
}
