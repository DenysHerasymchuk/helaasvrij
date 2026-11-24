namespace InternshipCoach.Infrastructure.Sources;

// Bound from the "GitHub" section (COACH_GitHub__Token). The OAuth keys live in the same section and are ignored here.
public sealed class GitHubIngestOptions
{
    public const string SectionName = "GitHub";

    public const string DefaultBaseUrl = "https://api.github.com/";

    // Server token: raises the REST limit from 60 to 5,000 requests per hour. Never log it.
    public string? Token { get; set; }

    public string BaseUrl { get; set; } = DefaultBaseUrl;
}
