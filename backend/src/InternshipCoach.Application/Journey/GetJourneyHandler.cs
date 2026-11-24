using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Applications;
using InternshipCoach.Application.Profile;
using InternshipCoach.Application.Sources;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Journey;

public sealed record JourneyPosting(Guid Id, string Company, string Title, int Score);

public sealed record JourneyAction(string Label, string Href);

/// <param name="CurrentStep">1 build profile, 2 pick a posting, 3 close gaps, 4 apply and track.</param>
/// <param name="Complete">True once the best posting is marked applied.</param>
public sealed record JourneyResult(
    int CurrentStep, bool Complete, bool HasSources, int ConfirmedItemCount, bool HasStagePeriod, int SavedPostingCount,
    int AnalysedPostingCount, JourneyPosting? BestPosting, int OpenGapCount, int DocumentCountForBest, bool BestOnBoard,
    bool BestApplied, int OnBoardCount, int AppliedCount, JourneyAction NextAction);

/// <summary>Where the student is on the path to an application, computed from existing data (repositories are user-scoped).</summary>
public sealed class GetJourneyHandler(
    ISourceRepository sources, IProfileRepository profile, IPostingRepository postings, IDocumentRepository documents,
    IApplicationRepository applications)
{
    public async Task<JourneyResult> HandleAsync(CancellationToken ct)
    {
        var hasSources = (await sources.ListAsync(ct)).Any(s => s.Status == SourceStatus.Ready);
        var confirmed = (await profile.ListItemsAsync(ct)).Count(i => i.Confirmed);
        var basics = await profile.FindBasicsAsync(ct);
        var hasStagePeriod = basics?.StageStart is not null && basics.StageEnd is not null;

        var allPostings = await postings.ListAsync(null, null, ct);
        var analysed = allPostings.Where(p => p.IsAnalysed).ToList();
        var best = analysed.OrderByDescending(p => p.Score()).FirstOrDefault();
        var openGaps = analysed.Sum(p => p.Requirements.Count(r => r.AnalysedAt is not null && r.Action is not null && !r.Done));

        // shortcut: documents have no posting link yet, so match the company name in the title; add Document.PostingId when tailoring needs it.
        var docsForBest = best is null
            ? 0
            : (await documents.ListAsync(ct)).Count(d => d.Title.Contains(best.Company, StringComparison.OrdinalIgnoreCase));

        var cards = await applications.ListAsync(ct);
        var applied = cards.Count(c => c.Application.Stage != ApplicationStage.Saved);
        var bestCard = best is null ? null : cards.FirstOrDefault(c => c.Application.PostingId == best.Id);
        var bestOnBoard = bestCard is not null;
        var bestApplied = bestCard is not null && bestCard.Application.Stage != ApplicationStage.Saved;

        // The gap step is done once the best posting has no open gap left, or the student ticked at least one.
        var gapsDone = best is not null
            && (best.Requirements.Any(r => r.Done) || best.Requirements.All(r => r.Action is null));

        var (step, next) = (hasSources, confirmed > 0, hasStagePeriod) switch
        {
            (false, _, _) => (1, new JourneyAction("Add your GitHub or CV", "/profile")),
            (_, false, _) => (1, new JourneyAction("Confirm your profile items", "/profile")),
            (_, _, false) => (1, new JourneyAction("Set your stage period", "/profile")),
            _ when allPostings.Count == 0 => (2, new JourneyAction("Find a posting", "/postings")),
            _ when best is null => (2, new JourneyAction($"Analyse {allPostings[0].Company}", $"/postings/{allPostings[0].Id}")),
            _ when !gapsDone => (3, new JourneyAction("Close your top gap", "/gaps")),
            _ when docsForBest == 0 => (4, new JourneyAction($"Tailor your CV for {best.Company}", $"/documents?postingId={best.Id}")),
            _ when !bestOnBoard => (4, new JourneyAction($"Add {best.Company} to applications", $"/postings/{best.Id}")),
            _ when !bestApplied => (4, new JourneyAction("Mark it applied", "/applications")),
            _ => (4, new JourneyAction("Practise interview questions", "/interview"))
        };

        return new JourneyResult(
            step, bestApplied, hasSources, confirmed, hasStagePeriod, allPostings.Count, analysed.Count,
            best is null ? null : new JourneyPosting(best.Id, best.Company, best.Title, best.Score()),
            openGaps, docsForBest, bestOnBoard, bestApplied, cards.Count, applied, next);
    }
}
