namespace InternshipCoach.Contracts.Sources;

public sealed record GitHubSyncRequest(string? Username);

public sealed record AddTextRequest(string? Label, string? Text);

public sealed record AddSiteRequest(string? Url);

public sealed record SourceResponse(
    Guid Id, string Kind, string Label, string Status, DateTimeOffset? LastSyncedAt, string? Error, int DocumentCount, int RepoCount);
