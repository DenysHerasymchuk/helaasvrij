using InternshipCoach.Application.Dashboard;
using InternshipCoach.Contracts.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/dashboard")]
[ApiController]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    [HttpGet]
    public async Task<DashboardResponse> Get([FromServices] GetDashboardHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(ct);
        return new DashboardResponse(
            result.Sources.Select(s => new DashboardSourceResponse(s.Kind, s.Label, s.Synced)).ToList(),
            result.Postings.Select(p => new DashboardPostingResponse(
                p.Id, p.Company, p.Title, p.City, p.Language, p.Deadline, p.InterviewAt, p.Score,
                p.Requirements.Select(r => new DashboardRequirementResponse(
                    r.Id, r.Text, r.Importance.ToString().ToLowerInvariant(), r.Coverage.ToString().ToLowerInvariant(),
                    r.Done, r.Action, r.Project, r.Due, r.Gain)).ToList())).ToList(),
            result.ApplicationsByMonth.Select(m => new DashboardMonthResponse(m.Month, m.Applied, m.Interview, m.Offer)).ToList());
    }
}
