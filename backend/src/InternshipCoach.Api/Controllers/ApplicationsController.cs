using InternshipCoach.Application.Applications;
using InternshipCoach.Contracts.Applications;
using InternshipCoach.Domain.Applications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/applications")]
[ApiController]
[Authorize]
public sealed class ApplicationsController : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ApplicationResponse>> List([FromServices] ListApplicationsHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(ct)).Select(ToResponse).ToList();

    [HttpGet("stats")]
    public async Task<IReadOnlyList<ApplicationMonthResponse>> Stats(
        [FromServices] GetApplicationStatsHandler handler, CancellationToken ct, [FromQuery] int? months = null) =>
        (await handler.HandleAsync(months, ct)).Select(m => new ApplicationMonthResponse(m.Month, m.Applied, m.Interview, m.Offer)).ToList();

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Create(CreateApplicationRequest request, [FromServices] CreateApplicationHandler handler, CancellationToken ct) =>
        Respond(await handler.HandleAsync(new CreateApplicationCommand(request.PostingId), ct), created: true);

    [HttpPatch("{id:guid}")]
    [Consumes("application/json")]
    public async Task<IActionResult> Update(
        Guid id, UpdateApplicationRequest request, [FromServices] UpdateApplicationHandler handler, CancellationToken ct) =>
        Respond(await handler.HandleAsync(new UpdateApplicationCommand(id, request.Stage, request.Notes), ct), created: false);

    private IActionResult Respond(ApplicationResult result, bool created) => result.Outcome switch
    {
        ApplicationOutcome.NotFound => NotFound(),
        ApplicationOutcome.Invalid => Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error),
        ApplicationOutcome.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: result.Error),
        _ => created ? StatusCode(StatusCodes.Status201Created, ToResponse(result.Card!)) : Ok(ToResponse(result.Card!))
    };

    private static ApplicationResponse ToResponse(ApplicationCard card)
    {
        var a = card.Application;
        return new ApplicationResponse(
            a.Id, a.PostingId, card.Company, card.Title, card.City, card.Deadline, a.Stage.ToCode(),
            a.AppliedAt, a.StageChangedAt, a.Notes, a.CreatedAt.Value);
    }
}
