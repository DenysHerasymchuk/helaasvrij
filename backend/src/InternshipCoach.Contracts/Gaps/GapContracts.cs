namespace InternshipCoach.Contracts.Gaps;

public sealed record GapResponse(
    Guid RequirementId, Guid PostingId, string Company, string PostingTitle, string Requirement, string Importance,
    string Coverage, string? Action, string? Project, DateOnly? DueDate, bool Done, decimal Impact, int ScoreGain, int PostingScore);

/// <summary>OldScore and NewScore are whole percentages of the posting, so the page can show "Brightlane 62% to 81%".</summary>
public sealed record RescanResponse(
    GapResponse Gap, int OldScore, int NewScore, string OldCoverage, string NewCoverage, int NewItemsToReview, int UnverifiableEvidence);
