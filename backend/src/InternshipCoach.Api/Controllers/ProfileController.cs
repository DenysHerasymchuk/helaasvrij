using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Profile.Extract;
using InternshipCoach.Application.Profile.Get;
using InternshipCoach.Application.Profile;
using InternshipCoach.Application.Profile.Items;
using InternshipCoach.Contracts.Profile;
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
        return Ok(new ProfileResponse(view.Items.Select(ToResponse).ToList()));
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

    [HttpDelete("items/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteItemHandler handler, CancellationToken ct) =>
        await handler.HandleAsync(id, ct) ? NoContent() : NotFound();

    private static ProfileItemResponse ToResponse(ProfileItemView item) => new(
        item.Id, item.Kind.ToString(), item.Name, item.Quote, item.Confirmed,
        item.Evidence.Select(e => new EvidenceResponse(e.Quote, e.SourceId, e.SourceKind.ToString(), e.SourceLabel, e.DocumentPath, e.Url)).ToList());
}
