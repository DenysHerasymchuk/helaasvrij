namespace InternshipCoach.Contracts.Calendar;

/// <param name="Type">interview, deadline, gapDue, stageStart or stageEnd.</param>
/// <param name="Date">yyyy-MM-dd in Belgian time.</param>
/// <param name="At">Exact UTC time, only for interviews.</param>
public sealed record CalendarEventResponse(string Type, DateOnly Date, DateTimeOffset? At, string Title, Guid PostingId, string Link);
