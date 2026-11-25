using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Sources;

public sealed record SourceSummary(
    Guid Id, SourceKind Kind, string Label, SourceStatus Status, DateTimeOffset? LastSyncedAt, string? Error, int DocumentCount, int RepoCount)
{
    public static SourceSummary From(Source source) =>
        new(source.Id, source.Kind, source.Label, source.Status, source.LastSyncedAt, source.Error, source.Documents.Count,
            source.Kind == SourceKind.GitHub ? source.Documents.Count(d => d.Path.EndsWith(RepoSummarySuffix)) : 0);

    /// <summary>Every synced repo gets exactly one "{repo}/summary" document, so counting them counts repos.</summary>
    public const string RepoSummarySuffix = "/summary";
}
