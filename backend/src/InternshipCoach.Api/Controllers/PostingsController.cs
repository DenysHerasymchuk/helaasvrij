using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Postings.Add;
using InternshipCoach.Application.Postings.Analyse;
using InternshipCoach.Application.Profile;
using InternshipCoach.Application.Profile.Get;
using InternshipCoach.Application.Postings.Browse;
using InternshipCoach.Contracts.Jobs;
using InternshipCoach.Application.Postings.Generic;
using InternshipCoach.Contracts.Postings;
using InternshipCoach.Domain.Common;
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
        [FromQuery] string? language, [FromQuery] string? q, [FromServices] ListPostingsHandler handler,
        [FromServices] GetBasicsHandler basics, CancellationToken ct)
    {
        var postings = await handler.HandleAsync(new ListPostingsQuery(language, q), ct);
        var stage = await basics.HandleAsync(ct);
        return Ok(postings.Select(p => ToSummary(p, stage)).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostingDetailResponse>> Get(
        Guid id, [FromServices] GetPostingHandler handler, [FromServices] ResolveEvidenceSourcesHandler sources,
        [FromServices] GetBasicsHandler basics, CancellationToken ct)
    {
        var posting = await handler.HandleAsync(id, ct);
        return posting is null ? NotFound() : Ok(ToDetail(posting, await sources.HandleAsync(posting, ct), await basics.HandleAsync(ct)));
    }

    [HttpPost("{id:guid}/analyse")]
    public async Task<IActionResult> Analyse(
        Guid id, [FromServices] AnalysePostingHandler handler, [FromServices] ResolveEvidenceSourcesHandler sources,
        [FromServices] GetBasicsHandler basics, CancellationToken ct)
    {
        try
        {
            var result = await handler.HandleAsync(id, ct);
            return result is null
                ? NotFound()
                : Ok(ToDetail(result.Posting, await sources.HandleAsync(result.Posting, ct), await basics.HandleAsync(ct), result.FromDemoCache));
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

    [HttpPost("{id:guid}/generic-answer")]
    public async Task<ActionResult<GenericAnswerResponse>> GenericAnswer(Guid id, [FromServices] GenericAnswerHandler handler, CancellationToken ct)
    {
        try
        {
            var posting = await handler.HandleAsync(id, ct);
            return posting is null
                ? NotFound()
                : Ok(new GenericAnswerResponse(posting.GenericAnswer!, posting.GenericAnswerAt!.Value, posting.IsDemo));
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
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "The generic answer failed. Try again.");
        }
    }

    [HttpPost("paste")]
    [Consumes("application/json")]
    public Task<IActionResult> Paste(
        PastePostingRequest request, [FromServices] AddPostingFromPasteHandler handler, [FromServices] GetBasicsHandler basics, CancellationToken ct) =>
        Add(() => handler.HandleAsync(new AddPostingFromPasteCommand(request.Text, request.Url), ct), basics, ct);

    [HttpPost("url")]
    [Consumes("application/json")]
    public Task<IActionResult> FromUrl(
        UrlPostingRequest request, [FromServices] AddPostingFromUrlHandler handler, [FromServices] GetBasicsHandler basics, CancellationToken ct) =>
        Add(() => handler.HandleAsync(new AddPostingFromUrlCommand(request.Url), ct), basics, ct);

    [HttpPost("from-job")]
    [Consumes("application/json")]
    public Task<IActionResult> FromJob(
        JobPostingRequest request, [FromServices] AddPostingFromJobHandler handler, [FromServices] GetBasicsHandler basics, CancellationToken ct) =>
        Add(() => handler.HandleAsync(new AddPostingFromJobCommand(request.JobId), ct), basics, ct);

    [HttpPut("{id:guid}/text")]
    [Consumes("application/json")]
    public Task<IActionResult> ReplaceText(
        Guid id, ReplacePostingTextRequest request, [FromServices] ReplacePostingTextHandler handler, [FromServices] GetBasicsHandler basics,
        CancellationToken ct) =>
        Add(() => handler.HandleAsync(new ReplacePostingTextCommand(id, request.Text), ct), basics, ct);

    // PUT is accepted next to PATCH because the shared api client has no PATCH helper.
    [HttpPatch("{id:guid}")]
    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    public async Task<ActionResult<PostingDetailResponse>> UpdateDates(
        Guid id, UpdatePostingDatesRequest request, [FromServices] UpdatePostingDatesHandler handler,
        [FromServices] ResolveEvidenceSourcesHandler sources, [FromServices] GetBasicsHandler basics, CancellationToken ct)
    {
        Posting? posting;
        try
        {
            posting = await handler.HandleAsync(
                new UpdatePostingDatesCommand(id, request.Deadline, request.InterviewAt, request.StageStart, request.StageEnd), ct);
        }
        catch (DomainException ex)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["stageEnd"] = [ex.Message] }));
        }

        return posting is null ? NotFound() : Ok(ToDetail(posting, await sources.HandleAsync(posting, ct), await basics.HandleAsync(ct)));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeletePostingHandler handler, CancellationToken ct) =>
        await handler.HandleAsync(id, ct) ? NoContent() : NotFound();

    private async Task<IActionResult> Add(Func<Task<PostingOutcome>> run, GetBasicsHandler basics, CancellationToken ct)
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
            PostingStatus.Created => Created($"/api/postings/{outcome.Posting!.Id}", ToDetail(outcome.Posting, null, await basics.HandleAsync(ct))),
            PostingStatus.Existing or PostingStatus.Updated => Ok(ToDetail(outcome.Posting!, null, await basics.HandleAsync(ct))),
            PostingStatus.Invalid => Problem(statusCode: StatusCodes.Status400BadRequest, title: outcome.Message),
            PostingStatus.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: outcome.Message),
            _ => Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: outcome.Message)
        };
    }

    private static PostingSummaryResponse ToSummary(Posting p, BasicsView stage) =>
        new(p.Id, p.Company, p.Title, p.City, p.Language.ToCode(), p.Deadline, p.InterviewAt, p.IsDemo,
            p.Requirements.Count, p.CreatedAt.Value, p.StageStart, p.StageEnd, Fit(p, stage));

    private static string Fit(Posting p, BasicsView stage) =>
        p.CheckStage(stage.StageStart, stage.StageEnd).ToString().ToLowerInvariant();

    private static PostingDetailResponse ToDetail(Posting p, IReadOnlyDictionary<Guid, DocumentRef>? sources, BasicsView stage, bool fromDemoCache = false) =>
        new(p.Id, p.Company, p.Title, p.City, p.Language.ToCode(), p.Url, p.RawText, p.Deadline, p.InterviewAt, p.IsDemo,
            p.CreatedAt.Value,
            p.Requirements.OrderBy(r => r.Position)
                .Select(r => new RequirementResponse(
                    r.Id, r.Text, r.Importance.ToString().ToLowerInvariant(), r.Coverage.ToString().ToLowerInvariant(),
                    r.EvidenceQuote, EvidenceSource(r, sources), r.Action, r.ActionProject, r.DueDate, r.Done, r.AnalysedAt)).ToList(),
            new GapAnalysisResponse(
                p.IsAnalysed, p.Score(), p.IsAnalysed && p.NoClearFit(), p.Requirements.Max(r => r.AnalysedAt), fromDemoCache),
            p.StageStart, p.StageEnd, Fit(p, stage));

    private static EvidenceSourceResponse? EvidenceSource(Requirement r, IReadOnlyDictionary<Guid, DocumentRef>? sources) =>
        r.EvidenceSourceDocumentId is { } id && sources is not null && sources.TryGetValue(id, out var d)
            ? new EvidenceSourceResponse(d.SourceId, d.SourceKind.ToString().ToLowerInvariant(), d.SourceLabel, d.Path, d.Url)
            : null;
}
