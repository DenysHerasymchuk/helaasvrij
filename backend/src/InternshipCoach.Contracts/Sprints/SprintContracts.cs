using InternshipCoach.Contracts.Applications;

namespace InternshipCoach.Contracts.Sprints;

public sealed record StartSprintRequest(string? Name, string? Goal, DateOnly StartsOn, DateOnly EndsOn);

/// <param name="Next">When set, open positions move into this new sprint; otherwise they go back to the backlog.</param>
public sealed record CompleteSprintRequest(StartSprintRequest? Next);

public sealed record SprintItemsRequest(IReadOnlyList<Guid> PostingIds);

/// <param name="Status">active or completed.</param>
/// <param name="Items">Positions in the sprint now.</param>
/// <param name="Open">Positions still saved, applied or in interview.</param>
/// <param name="Applied">Positions that reached applied or beyond during this sprint.</param>
public sealed record SprintResponse(
    Guid Id, string Name, string Goal, DateOnly StartsOn, DateOnly EndsOn, string Status, DateTimeOffset? CompletedAt,
    int Items, int Open, int Applied, int Interviews, int Offers, int Rejected);

public sealed record SprintReportItemResponse(
    Guid ApplicationId, Guid PostingId, string Key, string Company, string Title, string Stage, int? Score);

public sealed record SprintReportResponse(SprintResponse Sprint, IReadOnlyList<SprintReportItemResponse> Items, int? AverageScore, int GapsClosed);

public sealed record AddSprintItemsResponse(int Added);

/// <param name="Scope">Positions in the sprint that day.</param>
/// <param name="Open">Of those, the ones not yet at Offer or Rejected.</param>
public sealed record BurndownPointResponse(DateOnly Date, int Scope, int Open);

public sealed record CompleteSprintResponse(SprintResponse Completed, SprintResponse? Next, int Carried, int ReturnedToBacklog);
