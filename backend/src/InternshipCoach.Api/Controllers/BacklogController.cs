using InternshipCoach.Application.Sprints;
using InternshipCoach.Contracts.Postings;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Postings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/backlog")]
[ApiController]
[Authorize]
public sealed class BacklogController : ControllerBase
{
    /// <summary>Positions that are in no sprint. Optional <c>q</c> matches company, title or city.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<PostingSummaryResponse>> List(
        [FromQuery] string? q, [FromServices] ListBacklogHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(null, q, ct)).Select(item => PostingsController.ToSummary(item.Posting, item.Stage?.ToCode())).ToList();
}
