using InternshipCoach.Application.Sprints;
using InternshipCoach.Contracts.Sprints;
using InternshipCoach.Domain.Applications;
using InternshipCoach.Domain.Sprints;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/sprints")]
[ApiController]
[Authorize]
public sealed class SprintsController : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<SprintResponse>> List([FromServices] ListSprintsHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(ct)).Select(ToResponse).ToList();

    [HttpGet("{id:guid}/report")]
    public async Task<ActionResult<SprintReportResponse>> Report(Guid id, [FromServices] GetSprintReportHandler handler, CancellationToken ct)
    {
        var report = await handler.HandleAsync(id, ct);
        if (report is null)
        {
            return NotFound();
        }

        var items = report.Items.Select(item => new SprintReportItemResponse(
            item.Card.Application.Id, item.Card.Application.PostingId, item.Card.Key, item.Card.Company, item.Card.Title,
            item.Card.Application.Stage.ToCode(), item.Score)).ToList();
        return Ok(new SprintReportResponse(ToResponse(report.Summary), items, report.AverageScore, report.GapsClosed));
    }

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Start(StartSprintRequest request, [FromServices] StartSprintHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new StartSprintCommand(request.Name, request.Goal, request.StartsOn, request.EndsOn), ct);
        return result.Outcome == SprintOutcome.Ok
            ? StatusCode(StatusCodes.Status201Created, ToResponse(new SprintSummary(result.Value!, 0, 0, 0, 0, 0, 0)))
            : Fail(result.Outcome, result.Error);
    }

    [HttpPost("{id:guid}/complete")]
    [Consumes("application/json")]
    public async Task<IActionResult> Complete(Guid id, CompleteSprintRequest request, [FromServices] CompleteSprintHandler handler, CancellationToken ct)
    {
        var next = request.Next is { } n ? new StartSprintCommand(n.Name, n.Goal, n.StartsOn, n.EndsOn) : null;
        var result = await handler.HandleAsync(new CompleteSprintCommand(id, next), ct);
        if (result.Outcome != SprintOutcome.Ok)
        {
            return Fail(result.Outcome, result.Error);
        }

        var value = result.Value!;
        return Ok(new CompleteSprintResponse(
            ToResponse(value.Completed),
            value.Next is null ? null : ToResponse(new SprintSummary(value.Next, value.Carried, value.Carried, 0, 0, 0, 0)),
            value.Carried, value.ReturnedToBacklog));
    }

    [HttpPost("{id:guid}/items")]
    [Consumes("application/json")]
    public async Task<IActionResult> AddItems(Guid id, SprintItemsRequest request, [FromServices] AddSprintItemsHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, request.PostingIds ?? [], ct);
        return result.Outcome == SprintOutcome.Ok ? Ok(new AddSprintItemsResponse(result.Value!.Added)) : Fail(result.Outcome, result.Error);
    }

    [HttpDelete("{id:guid}/items/{postingId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid id, Guid postingId, [FromServices] RemoveSprintItemHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(id, postingId, ct);
        return result.Outcome == SprintOutcome.Ok ? NoContent() : Fail(result.Outcome, result.Error);
    }

    private IActionResult Fail(SprintOutcome outcome, string? error) => outcome switch
    {
        SprintOutcome.NotFound => NotFound(),
        SprintOutcome.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: error),
        _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: error)
    };

    private static SprintResponse ToResponse(SprintSummary s) => new(
        s.Sprint.Id, s.Sprint.Name, s.Sprint.Goal, s.Sprint.StartsOn, s.Sprint.EndsOn,
        s.Sprint.Status == SprintStatus.Active ? "active" : "completed", s.Sprint.CompletedAt,
        s.Items, s.Open, s.Applied, s.Interviews, s.Offers, s.Rejected);
}
