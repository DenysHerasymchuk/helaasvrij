namespace InternshipCoach.Contracts.Journey;

public sealed record JourneyPostingResponse(Guid Id, string Company, string Title, int Score);

public sealed record JourneyActionResponse(string Label, string Href);

/// <param name="CurrentStep">1 build profile, 2 pick a posting, 3 close gaps, 4 apply and track.</param>
/// <param name="Complete">True once the best posting is marked applied.</param>
public sealed record JourneyResponse(
    int CurrentStep, bool Complete, bool HasSources, int ConfirmedItemCount, bool HasStagePeriod, int SavedPostingCount,
    int AnalysedPostingCount, JourneyPostingResponse? BestPosting, int OpenGapCount, int DocumentCountForBest, bool BestOnBoard,
    bool BestApplied, int OnBoardCount, int AppliedCount, JourneyActionResponse NextAction);
