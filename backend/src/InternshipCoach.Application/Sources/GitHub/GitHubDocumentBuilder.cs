using System.Text;
using System.Text.Json;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Sources.GitHub;

/// <summary>Turns fetched repo data into source documents: one README and one summary per repo.</summary>
public static class GitHubDocumentBuilder
{
    public static void AddDocuments(Source source, IEnumerable<GitHubRepoData> repos)
    {
        foreach (var repo in repos)
        {
            if (!string.IsNullOrWhiteSpace(repo.Readme))
            {
                source.AddDocument($"{repo.Name}/README.md", repo.Readme, JsonSerializer.Serialize(new { repo = repo.Name, url = repo.Url }));
            }

            source.AddDocument($"{repo.Name}/summary", BuildSummary(repo), BuildMetadata(repo));
        }
    }

    public static string BuildSummary(GitHubRepoData repo)
    {
        var text = new StringBuilder();
        text.AppendLine($"Repository: {repo.Name}");
        if (!string.IsNullOrWhiteSpace(repo.Description))
        {
            text.AppendLine($"Description: {repo.Description}");
        }

        if (repo.PrimaryLanguage is not null)
        {
            text.AppendLine($"Primary language: {repo.PrimaryLanguage}");
        }

        if (repo.Languages.Count > 0)
        {
            var total = repo.Languages.Values.Sum();
            var parts = repo.Languages.OrderByDescending(pair => pair.Value)
                .Select(pair => total > 0 ? $"{pair.Key} {Math.Round(100.0 * pair.Value / total)}%" : pair.Key);
            text.AppendLine($"Languages: {string.Join(", ", parts)}");
        }

        if (repo.Topics.Count > 0)
        {
            text.AppendLine($"Topics: {string.Join(", ", repo.Topics)}");
        }

        text.AppendLine($"Commits: {repo.CommitCount}");
        text.AppendLine($"Dockerfile: {YesNo(repo.HasDockerfile)}");
        text.AppendLine($"Tests folder: {YesNo(repo.HasTests)}");
        text.AppendLine($"CI workflow: {YesNo(repo.HasCi)}");
        if (repo.TopLevelPaths.Count > 0)
        {
            text.AppendLine($"Files: {string.Join(", ", repo.TopLevelPaths)}");
        }

        return text.ToString().TrimEnd();
    }

    private static string BuildMetadata(GitHubRepoData repo) => JsonSerializer.Serialize(new
    {
        repo = repo.Name,
        url = repo.Url,
        language = repo.PrimaryLanguage,
        commits = repo.CommitCount,
        hasDockerfile = repo.HasDockerfile,
        hasTests = repo.HasTests,
        hasCi = repo.HasCi,
        pushedAt = repo.PushedAt
    });

    private static string YesNo(bool value) => value ? "yes" : "no";
}
