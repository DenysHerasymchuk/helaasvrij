namespace InternshipCoach.Application.Sources.GitHub;

public sealed record GitHubRepoData(
    string Name,
    string Url,
    string? Description,
    IReadOnlyList<string> Topics,
    string? PrimaryLanguage,
    IReadOnlyDictionary<string, long> Languages,
    string? Readme,
    IReadOnlyList<string> TopLevelPaths,
    bool HasDockerfile,
    bool HasTests,
    bool HasCi,
    int CommitCount,
    DateTimeOffset? PushedAt);
