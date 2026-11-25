using System.Text.RegularExpressions;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Sources.GitHub;

public sealed partial class SyncGitHubHandler(
    ISourceRepository sources, IGitHubClient github, IUserRepository users, ICurrentUser currentUser, IClock clock)
{
    public const int MaxRepos = 10;

    [GeneratedRegex("^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})$")]
    private static partial Regex UsernamePattern();

    public async Task<SourceSummary> HandleAsync(SyncGitHubCommand command, CancellationToken ct)
    {
        var username = command.Username?.Trim();
        if (string.IsNullOrEmpty(username))
        {
            var user = await users.FindByIdAsync(currentUser.UserId, ct);
            username = user?.GitHubLogin
                ?? throw new SourceInputException("Enter your GitHub username.");
        }

        if (!UsernamePattern().IsMatch(username))
        {
            throw new SourceInputException("That is not a valid GitHub username.");
        }

        var existing = await sources.FindByKindAsync(SourceKind.GitHub, ct);

        IReadOnlyList<GitHubRepoData> repos;
        try
        {
            repos = await github.FetchReposAsync(username, MaxRepos, ct);
            if (repos.Count == 0)
            {
                throw new SourceFetchException($"{username} has no public repositories that are not forks.");
            }
        }
        catch (SourceFetchException ex)
        {
            return await RecordFailureAsync(existing, username, ex.Message, ct);
        }

        var source = Source.Create(currentUser.UserId, SourceKind.GitHub, username);
        GitHubDocumentBuilder.AddDocuments(source, repos);
        source.MarkReady(clock.UtcNow);

        // The old copy goes only after the new one was fetched, so a failed re-sync keeps the previous data.
        if (existing is not null)
        {
            await sources.RemoveAsync(existing, ct);
        }

        await sources.AddAsync(source, ct);
        return SourceSummary.From(source);
    }

    private async Task<SourceSummary> RecordFailureAsync(Source? existing, string username, string error, CancellationToken ct)
    {
        if (existing is not null)
        {
            existing.MarkFailed(error, clock.UtcNow);
            await sources.SaveAsync(ct);
            return SourceSummary.From(existing);
        }

        var failed = Source.Create(currentUser.UserId, SourceKind.GitHub, username);
        failed.MarkFailed(error, clock.UtcNow);
        await sources.AddAsync(failed, ct);
        return SourceSummary.From(failed);
    }
}
