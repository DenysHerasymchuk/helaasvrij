namespace InternshipCoach.Application.Sources.List;

public sealed class ListSourcesHandler(ISourceRepository sources)
{
    public Task<IReadOnlyList<SourceSummary>> HandleAsync(CancellationToken ct) => sources.ListAsync(ct);
}
