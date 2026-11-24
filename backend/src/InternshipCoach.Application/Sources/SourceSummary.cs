using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Sources;

public sealed record SourceSummary(
    Guid Id, SourceKind Kind, string Label, SourceStatus Status, DateTimeOffset? LastSyncedAt, string? Error, int DocumentCount)
{
    public static SourceSummary From(Source source) =>
        new(source.Id, source.Kind, source.Label, source.Status, source.LastSyncedAt, source.Error, source.Documents.Count);
}
