using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Application.Profile;
using InternshipCoach.Application.Profile.Extract;
using InternshipCoach.Application.Sources;
using InternshipCoach.Application.Sources.GitHub;
using InternshipCoach.Domain.Postings;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Gaps;

public sealed record RescanResult(
    GapItem Gap, int OldScore, int NewScore, Coverage OldCoverage, Coverage NewCoverage, int NewItemsToReview, int UnverifiableEvidence);

/// <summary>
/// Ticks a gap: re-ingests only the action's repo into the existing GitHub source (confirmed profile items and
/// their evidence stay), reads the changed documents for new evidence, re-checks that one requirement and stores a
/// "Re-scan: ..." score snapshot. New profile items stay unconfirmed until the student confirms them.
/// </summary>
public sealed class MarkGapDoneHandler(
    IPostingRepository postings, ISourceRepository sources, IProfileRepository profile, IGitHubClient github,
    ExtractProfileHandler extract, RecheckRequirementHandler recheck, RescanThrottle throttle, IClock clock, ICurrentUser currentUser)
{
    public async Task<RescanResult?> HandleAsync(Guid requirementId, CancellationToken ct)
    {
        var posting = (await postings.ListAsync(null, null, ct)).FirstOrDefault(p => p.Requirements.Any(r => r.Id == requirementId));
        var requirement = posting?.Requirements.First(r => r.Id == requirementId);
        if (posting is null || requirement is null)
        {
            return null;
        }

        if (requirement.Done)
        {
            throw new GapInputException("This gap is already ticked. Undo it first to re-scan.");
        }

        if (string.IsNullOrWhiteSpace(requirement.Action) || string.IsNullOrWhiteSpace(requirement.ActionProject))
        {
            throw new GapInputException("This gap has no action to tick. Analyse the posting again.");
        }

        var gitHubSource = await sources.FindByKindAsync(SourceKind.GitHub, ct);
        if (gitHubSource is null)
        {
            throw new GapInputException("Connect your GitHub on the Profile page first, so the repo can be re-read.");
        }

        var wait = throttle.TryAcquire(currentUser.UserId, requirementId, clock.UtcNow);
        if (wait > 0)
        {
            throw new RescanThrottledException(wait);
        }

        var oldScore = posting.Score();
        var oldCoverage = requirement.Coverage;
        var reason = $"Re-scan: {requirement.Action}";

        var fresh = await github.FetchRepoAsync(gitHubSource.Label, requirement.ActionProject, ct);

        var referenced = await ReferencedDocumentsAsync(posting, ct);
        var added = GitHubRepoMerger.Merge(gitHubSource, fresh, referenced);
        await sources.SaveAsync(ct);

        var created = 0;
        if (added.Count > 0)
        {
            created = (await extract.HandleAsync(added, ct)).Created;
        }

        var unverifiable = GitHubRepoMerger.CountUnverifiable(
            await profile.ListItemsAsync(ct), await profile.ListReadyDocumentsAsync(ct));

        await recheck.HandleAsync(posting.Id, requirementId, ct, reason);

        // A re-analysis clears the tick; the student did tick it, so put it back (a now-strong row keeps no action).
        requirement.MarkDone();

        await postings.SaveAsync(ct);
        return new RescanResult(GapItem.From(posting, requirement), oldScore, posting.Score(), oldCoverage, requirement.Coverage, created, unverifiable);
    }

    // Documents that profile evidence or a requirement's evidence points at must survive the merge.
    private async Task<IReadOnlySet<Guid>> ReferencedDocumentsAsync(Posting current, CancellationToken ct)
    {
        var ids = new HashSet<Guid>();
        foreach (var item in await profile.ListItemsAsync(ct))
        {
            ids.Add(item.SourceDocumentId);
            ids.UnionWith(item.Evidence.Select(e => e.SourceDocumentId));
        }

        foreach (var posting in await postings.ListAsync(null, null, ct))
        {
            ids.UnionWith(posting.Requirements.Where(r => r.EvidenceSourceDocumentId is not null).Select(r => r.EvidenceSourceDocumentId!.Value));
        }

        return ids;
    }
}
