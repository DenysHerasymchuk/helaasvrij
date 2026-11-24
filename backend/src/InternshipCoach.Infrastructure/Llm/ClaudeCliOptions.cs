namespace InternshipCoach.Infrastructure.Llm;

public enum ClaudeCliMode
{
    /// <summary>Run the `claude` binary on this machine (dotnet run on the host).</summary>
    Local,

    /// <summary>POST to scripts/claude-bridge.mjs on the host (API inside Docker).</summary>
    Bridge
}

public sealed class ClaudeCliOptions
{
    public const string SectionName = "Llm:ClaudeCli";

    public ClaudeCliMode Mode { get; set; } = ClaudeCliMode.Local;

    public string Command { get; set; } = "claude";

    public string? BridgeUrl { get; set; }

    public string? Token { get; set; }

    public int TimeoutSeconds { get; set; } = 120;
}
