using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Gaps;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Application.Sources;
using InternshipCoach.Contracts.Gaps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/gaps")]
[ApiController]
[Authorize]
public sealed class GapsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GapResponse>>> List([FromServices] ListGapsHandler handler, CancellationToken ct) =>
        Ok((await handler.HandleAsync(ct)).Select(ToResponse).ToList());

    [HttpPost("{requirementId:guid}/done")]
    [Consumes("application/json")]
    public async Task<IActionResult> Done(Guid requirementId, [FromServices] MarkGapDoneHandler handler, CancellationToken ct)
    {
        try
        {
            var result = await handler.HandleAsync(requirementId, ct);
            return result is null
                ? NotFound()
                : Ok(new RescanResponse(
                    ToResponse(result.Gap), result.OldScore, result.NewScore, result.OldCoverage.ToString().ToLowerInvariant(),
                    result.NewCoverage.ToString().ToLowerInvariant(), result.NewItemsToReview, result.UnverifiableEvidence));
        }
        catch (GapInputException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message);
        }
        catch (RescanThrottledException ex)
        {
            Response.Headers.RetryAfter = ex.RetryAfterSeconds.ToString();
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: ex.Message);
        }
        catch (SourceFetchException ex)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: ex.Message);
        }
        catch (LlmDailyCapExceededException ex)
        {
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: ex.Message);
        }
        catch (LlmUnavailableException)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The AI service is not available right now. Try again later.");
        }
        catch (LlmException)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "The re-scan failed. Try again.");
        }
    }

    [HttpPost("{requirementId:guid}/undo")]
    [Consumes("application/json")]
    public async Task<ActionResult<GapResponse>> Undo(Guid requirementId, [FromServices] UndoGapHandler handler, CancellationToken ct)
    {
        var gap = await handler.HandleAsync(requirementId, ct);
        return gap is null ? NotFound() : Ok(ToResponse(gap));
    }

    private static GapResponse ToResponse(GapItem g) =>
        new(g.RequirementId, g.PostingId, g.Company, g.PostingTitle, g.Requirement, g.Importance.ToString().ToLowerInvariant(),
            g.Coverage.ToString().ToLowerInvariant(), g.Action, g.Project, g.DueDate, g.Done, g.Impact, g.ScoreGain, g.PostingScore);
}
