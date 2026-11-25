using System.Net;
using System.Net.Http.Headers;
using InternshipCoach.Application.Sources;
using InternshipCoach.Application.Sources.GitHub;

namespace InternshipCoach.Infrastructure.Sources;

public sealed class GitHubClient : IGitHubClient
{
    private const int MaxReadmeLength = 20_000;

    private readonly HttpClient _http;

    public GitHubClient(GitHubIngestOptions options, HttpMessageHandler? handler = null)
    {
        _http = handler is null ? new HttpClient() : new HttpClient(handler);
        _http.BaseAddress = new Uri(string.IsNullOrWhiteSpace(options.BaseUrl) ? GitHubIngestOptions.DefaultBaseUrl : options.BaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("InternshipCoach");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }
    }

    public async Task<IReadOnlyList<GitHubRepoData>> FetchReposAsync(string username, int max, CancellationToken ct)
    {
        var user = Uri.EscapeDataString(username);
        using var response = await SendAsync($"users/{user}/repos?type=owner&sort=pushed&direction=desc&per_page=100", null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SourceFetchException($"GitHub user '{username}' was not found.");
        }

        EnsureSuccess(response);

        var repos = GitHubJson.ParseRepos(await response.Content.ReadAsStringAsync(ct))
            .Where(repo => !repo.Fork)
            .Take(max)
            .ToList();

        return await Task.WhenAll(repos.Select(repo => FetchRepoAsync(username, repo, ct)));
    }

    public async Task<GitHubRepoData> FetchRepoAsync(string username, string repoName, CancellationToken ct)
    {
        using var response = await SendAsync($"repos/{Uri.EscapeDataString(username)}/{Uri.EscapeDataString(repoName)}", null, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new SourceFetchException($"Repository '{repoName}' was not found on GitHub.");
        }

        EnsureSuccess(response);

        var item = GitHubJson.ParseRepos($"[{await response.Content.ReadAsStringAsync(ct)}]").FirstOrDefault()
            ?? throw new SourceFetchException($"Repository '{repoName}' could not be read.");
        return await FetchRepoAsync(username, item, ct);
    }

    private async Task<GitHubRepoData> FetchRepoAsync(string owner, GitHubRepoListItem repo, CancellationToken ct)
    {
        var path = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo.Name)}";

        var languages = await TryGetAsync(
            $"{path}/languages", null, async r => GitHubJson.ParseLanguages(await r.Content.ReadAsStringAsync(ct)), ct)
            ?? new Dictionary<string, long>();

        var readme = await TryGetAsync($"{path}/readme", "application/vnd.github.raw+json", async r =>
        {
            var text = await r.Content.ReadAsStringAsync(ct);
            return text.Length > MaxReadmeLength ? text[..MaxReadmeLength] : text;
        }, ct);

        var tree = await TryGetAsync(
            $"{path}/git/trees/{Uri.EscapeDataString(repo.DefaultBranch)}?recursive=1", null,
            async r => GitHubJson.ParseTree(await r.Content.ReadAsStringAsync(ct)), ct)
            ?? new GitHubTreeInfo([], false, false, false);

        var commits = await TryGetAsync($"{path}/commits?per_page=1", null, async r =>
        {
            r.Headers.TryGetValues("Link", out var link);
            return GitHubJson.ParseCommitCount(link?.FirstOrDefault(), await r.Content.ReadAsStringAsync(ct));
        }, ct);

        return new GitHubRepoData(
            repo.Name, repo.Url, repo.Description, repo.Topics, repo.Language, languages, readme,
            tree.TopLevelPaths, tree.HasDockerfile, tree.HasTests, tree.HasCi, commits, repo.PushedAt);
    }

    // Per-repo extras are optional (no README, empty repo): a failure there must not fail the whole sync.
    private async Task<T?> TryGetAsync<T>(string url, string? accept, Func<HttpResponseMessage, Task<T>> read, CancellationToken ct)
    {
        try
        {
            using var response = await SendAsync(url, accept, ct);
            return response.IsSuccessStatusCode ? await read(response) : default;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException && !ct.IsCancellationRequested)
        {
            return default;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string url, string? accept, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (accept is not null)
        {
            request.Headers.Accept.Clear();
            request.Headers.Accept.ParseAdd(accept);
        }

        try
        {
            return await _http.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new SourceFetchException("Could not reach GitHub. Try again in a moment.");
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var rateLimited = response.StatusCode == HttpStatusCode.TooManyRequests
            || (response.StatusCode == HttpStatusCode.Forbidden
                && response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining) && remaining.FirstOrDefault() == "0");
        throw new SourceFetchException(rateLimited
            ? "GitHub rate limit reached. Try again later."
            : $"GitHub answered with an error ({(int)response.StatusCode}).");
    }
}
