using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Gaps;

/// <summary>All gaps across the student's postings: rows with an action, plus ticked rows. Biggest impact first, then due date.</summary>
public sealed class ListGapsHandler(IPostingRepository postings)
{
    public async Task<IReadOnlyList<GapItem>> HandleAsync(CancellationToken ct) =>
        (await postings.ListAsync(null, null, ct))
            .SelectMany(p => p.Requirements.Where(r => r.AnalysedAt is not null && (r.Action is not null || r.Done)).Select(r => GapItem.From(p, r)))
            .OrderBy(g => g.Done)
            .ThenByDescending(g => g.Impact)
            .ThenBy(g => g.DueDate ?? DateOnly.MaxValue)
            .ThenBy(g => g.Company, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

public sealed class UndoGapHandler(IPostingRepository postings)
{
    public async Task<GapItem?> HandleAsync(Guid requirementId, CancellationToken ct)
    {
        foreach (var posting in await postings.ListAsync(null, null, ct))
        {
            var requirement = posting.Requirements.FirstOrDefault(r => r.Id == requirementId);
            if (requirement is null)
            {
                continue;
            }

            requirement.Undo();
            await postings.SaveAsync(ct);
            return GapItem.From(posting, requirement);
        }

        return null;
    }
}
