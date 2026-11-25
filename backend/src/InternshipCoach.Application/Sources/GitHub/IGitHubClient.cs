namespace InternshipCoach.Application.Sources.GitHub;

public interface IGitHubClient
{
    /// <summary>Up to <paramref name="max" /> non-fork repos, most recently pushed first. Throws <see cref="SourceFetchException" />.</summary>
    Task<IReadOnlyList<GitHubRepoData>> FetchReposAsync(string username, int max, CancellationToken ct);

    /// <summary>One repo of <paramref name="username" /> by name. Throws <see cref="SourceFetchException" /> when it is missing or GitHub fails.</summary>
    Task<GitHubRepoData> FetchRepoAsync(string username, string repoName, CancellationToken ct);
}
