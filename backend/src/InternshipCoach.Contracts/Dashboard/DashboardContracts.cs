namespace InternshipCoach.Contracts.Dashboard;

/// <param name="Kind">GitHub, CV or Site.</param>
/// <param name="Synced">Null when the source is not connected yet.</param>
public sealed record DashboardSourceResponse(string Kind, string Label, DateTimeOffset? Synced);

/// <param name="Importance">required or nice.</param>
/// <param name="Coverage">strong, partial or none.</param>
/// <param name="Gain">Match points the posting gains when this requirement becomes strong.</param>
public sealed record DashboardRequirementResponse(
    Guid Id, string Text, string Importance, string Coverage, bool Done, string? Action, string? Project, DateOnly? Due, int Gain);

/// <param name="Language">NL, FR or EN.</param>
public sealed record DashboardPostingResponse(
    Guid Id, string Company, string Title, string City, string Language, DateOnly? Deadline, DateTimeOffset? InterviewAt,
    int Score, IReadOnlyList<DashboardRequirementResponse> Requirements);

public sealed record DashboardMonthResponse(string Month, int Applied, int Interview, int Offer);

public sealed record DashboardResponse(
    IReadOnlyList<DashboardSourceResponse> Sources, IReadOnlyList<DashboardPostingResponse> Postings,
    IReadOnlyList<DashboardMonthResponse> ApplicationsByMonth);
