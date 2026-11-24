using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Applications;
using InternshipCoach.Application.Sources;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Dashboard;

/// <param name="Synced">Last successful sync; null when the source is missing, pending or failed.</param>
public sealed record DashboardSource(string Kind, string Label, DateTimeOffset? Synced);

/// <param name="Gain">Match points the posting gains when this requirement becomes strong (same maths as the gap plan).</param>
public sealed record DashboardRequirement(
    Guid Id, string Text, Importance Importance, Coverage Coverage, bool Done, string? Action, string? Project, DateOnly? Due, int Gain);

public sealed record DashboardPosting(
    Guid Id, string Company, string Title, string City, string Language, DateOnly? Deadline, DateTimeOffset? InterviewAt,
    int Score, IReadOnlyList<DashboardRequirement> Requirements);

public sealed record DashboardResult(
    IReadOnlyList<DashboardSource> Sources, IReadOnlyList<DashboardPosting> Postings, IReadOnlyList<ApplicationMonth> ApplicationsByMonth);

/// <summary>Everything the dashboard shows, for the signed-in user only (the repositories are user-scoped).</summary>
public sealed class GetDashboardHandler(ISourceRepository sources, IPostingRepository postings, GetApplicationStatsHandler stats)
{
    // The three source cards the dashboard always shows; pasted text has no card.
    private static readonly (SourceKind Kind, string Name)[] Cards = [(SourceKind.GitHub, "GitHub"), (SourceKind.Cv, "CV"), (SourceKind.Site, "Site")];

    public async Task<DashboardResult> HandleAsync(CancellationToken ct)
    {
        var summaries = await sources.ListAsync(ct);
        var dashboardSources = Cards.Select(card =>
        {
            var found = summaries.FirstOrDefault(s => s.Kind == card.Kind);
            return found is null
                ? new DashboardSource(card.Name, "Not added yet", null)
                : new DashboardSource(card.Name, found.Label, found.Status == SourceStatus.Ready ? found.LastSyncedAt : null);
        }).ToList();

        var dashboardPostings = (await postings.ListAsync(null, null, ct)).Select(ToPosting).ToList();
        return new DashboardResult(dashboardSources, dashboardPostings, await stats.HandleAsync(null, ct));
    }

    private static DashboardPosting ToPosting(Posting posting) => new(
        posting.Id, posting.Company, posting.Title, posting.City, posting.Language.ToCode().ToUpperInvariant(),
        posting.Deadline, posting.InterviewAt, posting.Score(),
        posting.Requirements.OrderBy(r => r.Position).Select(r => new DashboardRequirement(
            r.Id, r.Text, r.Importance, r.Coverage, r.Done, r.Action, r.ActionProject, r.DueDate,
            Gaps.GapItem.From(posting, r).ScoreGain)).ToList());
}
