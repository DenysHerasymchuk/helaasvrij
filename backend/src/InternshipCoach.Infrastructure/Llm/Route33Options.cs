namespace InternshipCoach.Infrastructure.Llm;

public sealed class Route33Options
{
    public const string SectionName = "Route33";
    public const string DefaultBaseUrl = "https://api.route33.ai";

    public string? ApiKey { get; set; }

    /// <summary>Gateway base URL without /v1.</summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;
}
