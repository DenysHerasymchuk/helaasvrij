using InternshipCoach.Application.Tokens;
using InternshipCoach.Contracts.Tokens;
using InternshipCoach.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

// Tokens are managed from the signed-in browser session only: a token cannot create or revoke tokens.
[Route("api/tokens")]
[ApiController]
[Authorize(Policy = Auth.AuthSchemes.SessionOnlyPolicy)]
public sealed class TokensController : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ApiTokenResponse>> List([FromServices] ListApiTokensHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(ct)).Select(ToResponse).ToList();

    [HttpPost]
    [Consumes("application/json")]
    public async Task<IActionResult> Create(CreateApiTokenRequest request, [FromServices] CreateApiTokenHandler handler, CancellationToken ct)
    {
        try
        {
            var created = await handler.HandleAsync(request.Name, ct);
            return StatusCode(StatusCodes.Status201Created, new CreatedApiTokenResponse(ToResponse(created.Token), created.Secret));
        }
        catch (DomainException ex)
        {
            return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { ["name"] = [ex.Message] }));
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, [FromServices] RevokeApiTokenHandler handler, CancellationToken ct) =>
        await handler.HandleAsync(id, ct) ? NoContent() : NotFound();

    private static ApiTokenResponse ToResponse(ApiTokenView v) => new(v.Id, v.Name, v.Hint, v.CreatedAt, v.LastUsedAt, v.Revoked);
}
