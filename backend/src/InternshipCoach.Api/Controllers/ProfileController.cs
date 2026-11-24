using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Profile;
using InternshipCoach.Application.Profile.Basics;
using InternshipCoach.Application.Profile.Extract;
using InternshipCoach.Application.Profile.Get;
using InternshipCoach.Application.Profile.Items;
using InternshipCoach.Contracts.Profile;
using InternshipCoach.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/profile")]
[ApiController]
[Authorize]
public sealed class ProfileController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileResponse>> Get([FromServices] GetProfileHandler handler, CancellationToken ct)
    {
        var view = await handler.HandleAsync(ct);
        return Ok(new ProfileResponse(ToResponse(view.Basics), view.Items.Select(ToResponse).ToList()));
    }

    [HttpPost("extract")]
    public async Task<IActionResult> Extract([FromServices] ExtractProfileHandler handler, CancellationToken ct)
    {
        try
        {
            var result = await handler.HandleAsync(ct);
            return Ok(new ExtractResponse(result.Created, result.Merged, result.Dropped, result.Documents, result.SkippedDocuments));
        }
        catch (ProfileInputException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ex.Message);
        }
        catch (LlmDailyCapExceededException ex)
        {
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: ex.Message);
        }
        catch (LlmException)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "Building your profile failed because the AI service is unavailable. Try again.");
        }
    }

    [HttpPatch("items/{id:guid}")]
    [Consumes("application/json")]
    public async Task<IActionResult> SetConfirmed(
        Guid id, SetConfirmedRequest request, [FromServices] SetItemConfirmedHandler handler, CancellationToken ct) =>
        await handler.HandleAsync(id, request.Confirmed, ct) ? NoContent() : NotFound();

    [HttpPut("items/{id:guid}/level")]
    [Consumes("application/json")]
    public async Task<IActionResult> SetLevel(Guid id, SetLevelRequest request, [FromServices] SetItemLevelHandler handler, CancellationToken ct)
    {
        try
        {
            return await handler.HandleAsync(id, request.Level, ct) ? NoContent() : NotFound();
        }
        catch (DomainException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ex.Message);
        }
    }

    [HttpPost("items/confirm-all")]
    [Consumes("application/json")]
    public async Task<ActionResult<ConfirmAllResponse>> ConfirmAll([FromServices] ConfirmAllItemsHandler handler, CancellationToken ct) =>
        Ok(new ConfirmAllResponse(await handler.HandleAsync(ct)));

    [HttpDelete("items/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteItemHandler handler, CancellationToken ct) =>
        await handler.HandleAsync(id, ct) ? NoContent() : NotFound();

    [HttpPut("basics")]
    [Consumes("application/json")]
    public async Task<IActionResult> PutBasics(UpdateBasicsRequest request, [FromServices] UpdateBasicsHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new UpdateBasicsCommand(request.School, request.StageStart, request.StageEnd), ct);
        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(
                result.Errors!.ToDictionary(pair => pair.Key, pair => new[] { pair.Value })));
        }

        return Ok(ToResponse(result.Basics!));
    }

    private static BasicsResponse ToResponse(BasicsView basics) => new(basics.School, basics.StageStart, basics.StageEnd);

    private static ProfileItemResponse ToResponse(ProfileItemView item) => new(
        item.Id, item.Kind.ToString(), item.Name, item.Level, item.Quote, item.Confirmed,
        item.Evidence.Select(e => new EvidenceResponse(e.Quote, e.SourceId, e.SourceKind.ToString(), e.SourceLabel, e.DocumentPath, e.Url)).ToList());
}
