using InternshipCoach.Application.Sources;
using InternshipCoach.Application.Sources.Cv;
using InternshipCoach.Application.Sources.Delete;
using InternshipCoach.Application.Sources.GitHub;
using InternshipCoach.Application.Sources.List;
using InternshipCoach.Application.Sources.Site;
using InternshipCoach.Application.Sources.Text;
using InternshipCoach.Contracts.Sources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/sources")]
[ApiController]
[Authorize]
public sealed class SourcesController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SourceResponse>>> List(
        [FromServices] ListSourcesHandler handler, CancellationToken ct) =>
        Ok((await handler.HandleAsync(ct)).Select(ToResponse).ToList());

    [HttpPost("github")]
    [Consumes("application/json")]
    public Task<IActionResult> SyncGitHub(
        GitHubSyncRequest request, [FromServices] SyncGitHubHandler handler, CancellationToken ct) =>
        RunAsync(() => handler.HandleAsync(new SyncGitHubCommand(request.Username), ct));

    // Multipart (not JSON) because it carries a file. SameSite=Lax keeps cross-site form posts from carrying the cookie.
    [HttpPost("cv")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public Task<IActionResult> UploadCv(
        IFormFile? file, [FromServices] UploadCvHandler handler, CancellationToken ct) =>
        RunAsync(async () =>
        {
            if (file is null || file.Length == 0)
            {
                throw new SourceInputException("Choose a PDF file.");
            }

            if (file.Length > UploadCvHandler.MaxBytes)
            {
                throw new SourceInputException("The PDF is larger than 5 MB.");
            }

            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, ct);
            return await handler.HandleAsync(new UploadCvCommand(file.FileName, buffer.ToArray()), ct);
        });

    [HttpPost("text")]
    [Consumes("application/json")]
    public Task<IActionResult> AddText(
        AddTextRequest request, [FromServices] AddTextHandler handler, CancellationToken ct) =>
        RunAsync(() => handler.HandleAsync(new AddTextCommand(request.Label, request.Text), ct));

    [HttpPost("site")]
    [Consumes("application/json")]
    public Task<IActionResult> AddSite(
        AddSiteRequest request, [FromServices] AddSiteHandler handler, CancellationToken ct) =>
        RunAsync(() => handler.HandleAsync(new AddSiteCommand(request.Url), ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteSourceHandler handler, CancellationToken ct) =>
        await handler.HandleAsync(id, ct) ? NoContent() : NotFound();

    private async Task<IActionResult> RunAsync(Func<Task<SourceSummary>> action)
    {
        try
        {
            return Ok(ToResponse(await action()));
        }
        catch (SourceInputException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ex.Message);
        }
    }

    private static SourceResponse ToResponse(SourceSummary source) =>
        new(source.Id, source.Kind.ToString(), source.Label, source.Status.ToString(), source.LastSyncedAt, source.Error, source.DocumentCount);
}
