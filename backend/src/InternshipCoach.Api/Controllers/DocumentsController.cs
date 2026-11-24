using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Documents.DeleteDocument;
using InternshipCoach.Application.Documents.GetDocument;
using InternshipCoach.Application.Documents.ListDocuments;
using InternshipCoach.Application.Documents.TailorCv;
using InternshipCoach.Application.Documents.UpdateDocument;
using InternshipCoach.Contracts.Documents;
using InternshipCoach.Domain.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/documents")]
[ApiController]
[Authorize]
public sealed class DocumentsController : ControllerBase
{
    [HttpPost("cv/tailor")]
    [Consumes("application/json")]
    public async Task<IActionResult> TailorCv(TailorCvRequest request, [FromServices] TailorCvHandler handler, CancellationToken ct)
    {
        TailorCvResult result;
        try
        {
            result = await handler.HandleAsync(new TailorCvCommand(request.CvText, request.PostingText, request.Language), ct);
        }
        catch (LlmDailyCapExceededException ex)
        {
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: ex.Message);
        }
        catch (LlmException)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "The AI service did not answer. Try again.");
        }

        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(result.Errors!.ToDictionary(pair => pair.Key, pair => pair.Value)));
        }

        return Ok(ToResponse(result.Document!, result.DroppedCount));
    }

    [HttpGet]
    public async Task<IReadOnlyList<DocumentSummaryResponse>> List([FromServices] ListDocumentsHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(ct))
        .Select(d => new DocumentSummaryResponse(d.Id, Kind(d.Kind), d.Language, d.Title, d.CreatedAt.Value, d.UpdatedAt))
        .ToList();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromServices] GetDocumentHandler handler, CancellationToken ct) =>
        await handler.HandleAsync(id, ct) is { } document ? Ok(ToResponse(document, null)) : NotFound();

    [HttpPut("{id:guid}")]
    [Consumes("application/json")]
    public async Task<IActionResult> Update(
        Guid id, UpdateDocumentRequest request, [FromServices] UpdateDocumentHandler handler, CancellationToken ct)
    {
        var result = await handler.HandleAsync(new UpdateDocumentCommand(id, request.Title, request.Content), ct);
        if (result.Error is not null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
        }

        return result.Document is null ? NotFound() : Ok(ToResponse(result.Document, null));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteDocumentHandler handler, CancellationToken ct) =>
        await handler.HandleAsync(id, ct) ? NoContent() : NotFound();

    private static string Kind(DocumentKind kind) => kind switch
    {
        DocumentKind.Cv => "cv",
        DocumentKind.CoverLetter => "coverLetter",
        _ => "recruiterMessage"
    };

    private static DocumentResponse ToResponse(Document d, int? droppedCount) =>
        new(d.Id, Kind(d.Kind), d.Language, d.Title, d.Content, d.CreatedAt.Value, d.UpdatedAt, droppedCount);
}
