using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Calendar;
using InternshipCoach.Contracts.Calendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/calendar")]
[ApiController]
[Authorize]
public sealed class CalendarController : ControllerBase
{
    /// <summary>Events between from and to (inclusive). Defaults: today to 60 days later.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CalendarEventResponse>>> Get(
        [FromServices] GetCalendarHandler handler, [FromServices] IClock clock, CancellationToken ct,
        [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null)
    {
        var start = from ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var end = to ?? start.AddDays(60);
        if (end < start || end.DayNumber - start.DayNumber > GetCalendarHandler.MaxDays)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"'to' must be on or after 'from' and at most {GetCalendarHandler.MaxDays} days later.");
        }

        var events = await handler.HandleAsync(start, end, ct);
        return Ok(events.Select(e => new CalendarEventResponse(
            char.ToLowerInvariant(e.Type.ToString()[0]) + e.Type.ToString()[1..], e.Date, e.At, e.Title, e.PostingId, e.Link)).ToList());
    }
}
