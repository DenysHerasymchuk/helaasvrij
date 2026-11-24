using System.Text.Json;
using System.Text.RegularExpressions;

namespace InternshipCoach.Infrastructure.Sources;

public sealed record GitHubRepoListItem(
    string Name, string Url, string? Description, IReadOnlyList<string> Topics, string? Language,
    bool Fork, string DefaultBranch, DateTimeOffset? PushedAt);

public sealed record GitHubTreeInfo(IReadOnlyList<string> TopLevelPaths, bool HasDockerfile, bool HasTests, bool HasCi);

/// <summary>Pure mapping from GitHub REST JSON to our records. No network, so it is tested against recorded fixtures.</summary>
public static partial class GitHubJson
{
    private const int MaxListedPaths = 60;

    private static readonly string[] TestFolderNames = ["test", "tests", "__tests__", "spec", "specs"];

    [GeneratedRegex(@"<[^>]*[?&]page=(\d+)[^>]*>;\s*rel=""last""")]
    private static partial Regex LastPagePattern();

    public static IReadOnlyList<GitHubRepoListItem> ParseRepos(string json)
    {
        using var document = JsonDocument.Parse(json);
        var repos = new List<GitHubRepoListItem>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var topics = item.TryGetProperty("topics", out var topicsElement) && topicsElement.ValueKind == JsonValueKind.Array
                ? topicsElement.EnumerateArray().Select(topic => topic.GetString()).OfType<string>().ToList()
                : [];

            repos.Add(new GitHubRepoListItem(
                Name: item.GetProperty("name").GetString()!,
                Url: GetString(item, "html_url") ?? string.Empty,
                Description: GetString(item, "description"),
                Topics: topics,
                Language: GetString(item, "language"),
                Fork: item.TryGetProperty("fork", out var fork) && fork.ValueKind == JsonValueKind.True,
                DefaultBranch: GetString(item, "default_branch") ?? "main",
                PushedAt: DateTimeOffset.TryParse(GetString(item, "pushed_at"), out var pushed) ? pushed : null));
        }

        return repos;
    }

    public static IReadOnlyDictionary<string, long> ParseLanguages(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.Number)
            .ToDictionary(property => property.Name, property => property.Value.GetInt64());
    }

    // Looks at the top two levels of the tree only; deeper entries are ignored.
    public static GitHubTreeInfo ParseTree(string json)
    {
        using var document = JsonDocument.Parse(json);
        var paths = new List<string>();
        if (document.RootElement.TryGetProperty("tree", out var tree))
        {
            foreach (var entry in tree.EnumerateArray())
            {
                var path = GetString(entry, "path");
                if (path is not null && path.Split('/').Length <= 2)
                {
                    paths.Add(entry.TryGetProperty("type", out var type) && type.GetString() == "tree" ? path + "/" : path);
                }
            }
        }

        bool Any(Func<string, bool> predicate) => paths.Any(path => predicate(path.TrimEnd('/')));

        var hasDockerfile = Any(path =>
        {
            var name = path[(path.LastIndexOf('/') + 1)..];
            return name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("Dockerfile.", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("docker-compose", StringComparison.OrdinalIgnoreCase);
        });

        var hasTests = paths.Any(path => path.EndsWith('/')
            && TestFolderNames.Contains(path.TrimEnd('/').Split('/')[^1], StringComparer.OrdinalIgnoreCase));

        var hasCi = Any(path =>
            path.StartsWith(".github/workflows", StringComparison.Ordinal)
            || path.StartsWith(".circleci", StringComparison.Ordinal)
            || path is ".gitlab-ci.yml" or "Jenkinsfile" or ".travis.yml" or "azure-pipelines.yml");

        return new GitHubTreeInfo(paths.Take(MaxListedPaths).ToList(), hasDockerfile, hasTests, hasCi);
    }

    // Asked with per_page=1, so the page number of rel="last" is the commit count.
    public static int ParseCommitCount(string? linkHeader, string bodyJson)
    {
        if (!string.IsNullOrEmpty(linkHeader))
        {
            var match = LastPagePattern().Match(linkHeader);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var pages))
            {
                return pages;
            }
        }

        using var document = JsonDocument.Parse(bodyJson);
        return document.RootElement.ValueKind == JsonValueKind.Array ? document.RootElement.GetArrayLength() : 0;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
