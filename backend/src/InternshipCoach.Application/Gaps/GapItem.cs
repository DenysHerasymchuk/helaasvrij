using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Gaps;

/// <summary>One checklist row: a requirement of a posting that has an action (or was ticked).</summary>
public sealed record GapItem(
    Guid RequirementId, Guid PostingId, string Company, string PostingTitle, string Requirement, Importance Importance,
    Coverage Coverage, string? Action, string? Project, DateOnly? DueDate, bool Done, decimal Impact, int ScoreGain, int PostingScore)
{
    /// <summary>Impact = weight x (1 - coverage), so a missing required item is 2, a partial required item 1.</summary>
    public static GapItem From(Posting posting, Requirement r)
    {
        var missingHalfUnits = 2 - r.Coverage.HalfUnits();
        var total = posting.Requirements.Sum(x => x.Importance.Weight());
        var gain = total == 0 ? 0 : (int)Math.Round(100m * r.Importance.Weight() * missingHalfUnits / (2m * total), MidpointRounding.AwayFromZero);
        return new GapItem(
            r.Id, posting.Id, posting.Company, posting.Title, r.Text, r.Importance, r.Coverage, r.Action, r.ActionProject,
            r.DueDate, r.Done, r.Importance.Weight() * missingHalfUnits / 2m, gain, posting.Score());
    }
}
