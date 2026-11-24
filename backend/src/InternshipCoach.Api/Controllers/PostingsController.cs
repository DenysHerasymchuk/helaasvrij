using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Postings.Add;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Application.Profile;
using InternshipCoach.Application.Postings.Browse;
using InternshipCoach.Contracts.Postings;
using InternshipCoach.Domain.Postings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/postings")]
[ApiController]
[Authorize]
public sealed class PostingsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PostingSummaryResponse>>> List(
        [FromQuery] string? language, [FromQuery] string? q, [FromServices] ListPostingsHandler handler, CancellationToken ct)
    {
        var postings = await handler.HandleAsync(new ListPostingsQuery(language, q), ct);
        return Ok(postings.Select(ToSummary).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostingDetailResponse>> Get(
        Guid id, [FromServices] GetPostingHandler handler, [FromServices] ResolveEvidenceSourcesHandler sources, CancellationToken ct)
    {
        var posting = await handler.HandleAsync(id, ct);
        return posting is null ? NotFound() : Ok(ToDetail(posting, await sources.HandleAsync(posting, ct)));
    }

    [HttpPost("{id:guid}/analyse")]
    public async Task<IActionResult> Analyse(
        Guid id, [FromServices] AnalysePostingHandler handler, [FromServices] ResolveEvidenceSourcesHandler sources, CancellationToken ct)
    {
        try
        {
            var posting = await handler.HandleAsync(id, ct);
            return posting is null ? NotFound() : Ok(ToDetail(posting, await sources.HandleAsync(posting, ct)));
        }
        catch (GapInputException ex)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: ex.Message);
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
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "The analysis failed. Try again.");
        }
    }

    [HttpPost("paste")]
    [Consumes("application/json")]
    public Task<IActionResult> Paste(PastePostingRequest request, [FromServices] AddPostingFromPasteHandler handler, CancellationToken ct) =>
        Add(() => handler.HandleAsync(new AddPostingFromPasteCommand(request.Text, request.Url), ct));

    [HttpPost("url")]
    [Consumes("application/json")]
    public Task<IActionResult> FromUrl(UrlPostingRequest request, [FromServices] AddPostingFromUrlHandler handler, CancellationToken ct) =>
        Add(() => handler.HandleAsync(new AddPostingFromUrlCommand(request.Url), ct));

    // PUT is accepted next to PATCH because the shared api client has no PATCH helper.
    [HttpPatch("{id:guid}")]
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    public async Task<ActionResult<PostingDetailResponse>> UpdateDates(
        Guid id, UpdatePostingDatesRequest request, [FromServices] UpdatePostingDatesHandler handler,
        [FromServices] ResolveEvidenceSourcesHandler sources, CancellationToken ct)
    {
        var posting = await handler.HandleAsync(new UpdatePostingDatesCommand(id, request.Deadline, request.InterviewAt), ct);
        return posting is null ? NotFound() : Ok(ToDetail(posting, await sources.HandleAsync(posting, ct)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeletePostingHandler handler, CancellationToken ct) =>
        await handler.HandleAsync(id, ct) ? NoContent() : NotFound();

    private async Task<IActionResult> Add(Func<Task<PostingOutcome>> run)
    {
        PostingOutcome outcome;
        try
        {
            outcome = await run();
        }
        catch (LlmDailyCapExceededException ex)
        {
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: ex.Message);
        }
        catch (LlmUnavailableException)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The AI service is not available right now. Try again later.");
        }
        catch (LlmInvalidResponseException)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "We could not read that posting. Check the text and try again.");
        }

        return outcome.Status switch
        {
            PostingStatus.Created => Created($"/api/postings/{outcome.Posting!.Id}", ToDetail(outcome.Posting, null)),
            PostingStatus.Invalid => Problem(statusCode: StatusCodes.Status400BadRequest, title: outcome.Message),
            _ => Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: outcome.Message)
        };
    }

    private static PostingSummaryResponse ToSummary(Posting p) =>
        new(p.Id, p.Company, p.Title, p.City, p.Language.ToCode(), p.Deadline, p.InterviewAt, p.IsDemo,
            p.Requirements.Count, p.CreatedAt.Value);

    private static PostingDetailResponse ToDetail(Posting p, IReadOnlyDictionary<Guid, DocumentRef>? sources) =>
        new(p.Id, p.Company, p.Title, p.City, p.Language.ToCode(), p.Url, p.RawText, p.Deadline, p.InterviewAt, p.IsDemo,
            p.CreatedAt.Value,
            p.Requirements.OrderBy(r => r.Position)
                .Select(r => new RequirementResponse(
                    r.Id, r.Text, r.Importance.ToString().ToLowerInvariant(), r.Coverage.ToString().ToLowerInvariant(),
                    r.EvidenceQuote, EvidenceSource(r, sources), r.Action, r.ActionProject, r.DueDate, r.Done, r.AnalysedAt)).ToList(),
            new GapAnalysisResponse(
                p.IsAnalysed, p.Score(), p.IsAnalysed && p.NoClearFit(), p.Requirements.Max(r => r.AnalysedAt)));

    private static EvidenceSourceResponse? EvidenceSource(Requirement r, IReadOnlyDictionary<Guid, DocumentRef>? sources) =>
        r.EvidenceSourceDocumentId is { } id && sources is not null && sources.TryGetValue(id, out var d)
            ? new EvidenceSourceResponse(d.SourceId, d.SourceKind.ToString().ToLowerInvariant(), d.SourceLabel, d.Path, d.Url)
            : null;
}
