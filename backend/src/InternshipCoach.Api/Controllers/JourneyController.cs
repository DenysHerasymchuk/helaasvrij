using InternshipCoach.Application.Journey;
using InternshipCoach.Contracts.Journey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/journey")]
[ApiController]
[Authorize]
public sealed class JourneyController : ControllerBase
{
    [HttpGet]
    public async Task<JourneyResponse> Get([FromServices] GetJourneyHandler handler, CancellationToken ct)
    {
        var r = await handler.HandleAsync(ct);
        return new JourneyResponse(
            r.CurrentStep, r.Complete, r.HasSources, r.ConfirmedItemCount, r.SavedPostingCount, r.AnalysedPostingCount,
            r.BestPosting is { } b ? new JourneyPostingResponse(b.Id, b.Company, b.Title, b.Score) : null,
            r.OpenGapCount, r.DocumentCountForBest, r.BestOnBoard, r.BestApplied, r.OnBoardCount, r.AppliedCount,
            new JourneyActionResponse(r.NextAction.Label, r.NextAction.Href));
    }
}
