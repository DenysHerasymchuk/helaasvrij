namespace InternshipCoach.Infrastructure.Llm.Replay;

/// <summary>
/// Demo mode. COACH_Demo__Replay=true replays stored LLM responses; COACH_Demo__Record=true also stores misses.
/// Both default to off. The recording holds model output derived from real CV text, so it must stay in a git-ignored folder.
/// </summary>
public sealed class DemoReplayOptions
{
    public const string SectionName = "Demo";
    public const string DefaultPath = "demo-recordings/llm-replay.json";

    public bool Replay { get; set; }

    public bool Record { get; set; }

    public string Path { get; set; } = DefaultPath;
}
