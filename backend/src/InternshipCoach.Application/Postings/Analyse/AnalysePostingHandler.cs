using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Analyse;

/// <summary>
/// Runs the gap matrix for a posting: loads the confirmed evidence, lets the model label coverage, applies the code
/// checks, stores the result and a score snapshot. At most <see cref="GapAnalyser.MaxCalls" /> model calls.
/// </summary>
public sealed class AnalysePostingHandler(
    IPostingRepository postings, GapEvidenceLoader loader, GapAnalyser analyser, IClock clock, DemoSafetyOptions demo)
{
    public async Task<AnalysePostingResult?> HandleAsync(Guid postingId, CancellationToken ct)
    {
        var posting = await postings.FindAsync(postingId, ct);
        if (posting is null)
        {
            return null;
        }

        var evidence = await loader.LoadAsync(ct);
        if (evidence.Quotes.Count == 0)
        {
            throw new GapInputException("Read your sources on your profile first. The analysis only uses items from your profile.");
        }

        var requirements = posting.Requirements.OrderBy(r => r.Position).ToList();
        IReadOnlyList<RequirementVerdict> verdicts;
        if (demo.Enabled && posting.IsDemo && posting.IsAnalysed)
        {
            // Demo only: keep the stored analysis when the model is down or slower than the timeout.
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(demo.TimeoutSeconds));
            try
            {
                verdicts = await analyser.AnalyseAsync(requirements, posting.Language, evidence, timeout.Token);
            }
            catch (Exception ex) when ((ex is LlmException || ex is OperationCanceledException) && !ct.IsCancellationRequested)
            {
                return new AnalysePostingResult(posting, FromDemoCache: true);
            }
        }
        else
        {
            verdicts = await analyser.AnalyseAsync(requirements, posting.Language, evidence, ct);
        }

        var byId = requirements.ToDictionary(r => r.Id);

        var now = clock.UtcNow;
        var due = GapPlan.DueDates(verdicts.Select(v => (byId[v.RequirementId], v.Coverage)), DateOnly.FromDateTime(now.UtcDateTime));
        foreach (var verdict in verdicts)
        {
            byId[verdict.RequirementId].RecordAnalysis(
                verdict.Coverage, verdict.Quote, verdict.DocumentId, verdict.Action, verdict.Project,
                due.TryGetValue(verdict.RequirementId, out var date) ? date : null, now);
        }

        posting.RecordSnapshot("analyse", now);
        await postings.SaveAsync(ct);
        return new AnalysePostingResult(posting, FromDemoCache: false);
    }
}

/// <summary>
/// Re-checks one requirement against freshly loaded evidence (after a source was re-synced) and stores a "rescan"
/// snapshot. The due date and the other requirements stay as they are.
/// </summary>
public sealed class RecheckRequirementHandler(IPostingRepository postings, GapEvidenceLoader loader, GapAnalyser analyser, IClock clock)
{
    public async Task<Posting?> HandleAsync(Guid postingId, Guid requirementId, CancellationToken ct, string reason = "rescan")
    {
        var posting = await postings.FindAsync(postingId, ct);
        var requirement = posting?.Requirements.FirstOrDefault(r => r.Id == requirementId);
        if (posting is null || requirement is null)
        {
            return null;
        }

        var evidence = await loader.LoadAsync(ct);
        if (evidence.Quotes.Count == 0)
        {
            throw new GapInputException("Read your sources on your profile first. The analysis only uses items from your profile.");
        }

        var verdict = (await analyser.AnalyseAsync([requirement], posting.Language, evidence, ct)).Single();
        var now = clock.UtcNow;
        requirement.RecordAnalysis(
            verdict.Coverage, verdict.Quote, verdict.DocumentId, verdict.Action, verdict.Project,
            verdict.Coverage == Coverage.Strong ? null : requirement.DueDate, now);

        posting.RecordSnapshot(reason, now);
        await postings.SaveAsync(ct);
        return posting;
    }
}
