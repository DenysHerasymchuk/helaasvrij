using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Applications;
using InternshipCoach.Domain.Applications;

namespace InternshipCoach.Application.Calendar;

public enum CalendarEventType
{
    Interview,
    Deadline,
    GapDue
}

/// <param name="Date">The day in Belgian time.</param>
/// <param name="At">Exact time, only for interviews.</param>
public sealed record CalendarEvent(CalendarEventType Type, DateOnly Date, DateTimeOffset? At, string Title, Guid PostingId, string Link);

/// <summary>Dates from the student's own postings between From and To (both inclusive), soonest first.</summary>
public sealed class GetCalendarHandler(IPostingRepository postings, IApplicationRepository applications, ICurrentUser currentUser)
{
    public const int MaxDays = 400;

    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    public async Task<IReadOnlyList<CalendarEvent>> HandleAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rejected = (await applications.ListAsync(ct))
            .Where(c => c.Application.Stage == ApplicationStage.Rejected)
            .Select(c => c.Application.PostingId)
            .ToHashSet();

        var events = new List<CalendarEvent>();
        // The data layer already filters by user; the check here keeps it true for any repository.
        foreach (var p in (await postings.ListAsync(null, null, ct)).Where(p => p.UserId == currentUser.UserId))
        {
            var link = $"/postings/{p.Id}";
            if (p.InterviewAt is { } at)
            {
                var local = TimeZoneInfo.ConvertTime(at, Brussels);
                events.Add(new CalendarEvent(CalendarEventType.Interview, DateOnly.FromDateTime(local.DateTime), at, $"Interview at {p.Company}", p.Id, link));
            }

            if (p.Deadline is { } deadline && !rejected.Contains(p.Id))
            {
                events.Add(new CalendarEvent(CalendarEventType.Deadline, deadline, null, $"Application deadline: {p.Company}", p.Id, link));
            }

            foreach (var r in p.Requirements.Where(r => r is { Action: not null, Done: false, DueDate: not null, AnalysedAt: not null }))
            {
                events.Add(new CalendarEvent(CalendarEventType.GapDue, r.DueDate!.Value, null, $"{r.Action} ({p.Company})", p.Id, $"/gaps?postingId={p.Id}"));
            }
        }

        return events
            .Where(e => e.Date >= from && e.Date <= to)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.At ?? DateTimeOffset.MaxValue)
            .ThenBy(e => e.Type)
            .ToList();
    }
}
