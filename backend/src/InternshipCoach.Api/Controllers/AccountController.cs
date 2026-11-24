using InternshipCoach.Api.Auth;
using InternshipCoach.Application.Account.Delete;
using InternshipCoach.Contracts.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/me")]
[ApiController]
[Authorize]
public sealed class AccountController : ControllerBase
{
    // Deletes every row the user owns plus the user, then signs out.
    [HttpDelete]
    [Consumes("application/json")]
    public async Task<IActionResult> Delete(
        DeleteAccountRequest request, [FromServices] DeleteAccountHandler handler, CancellationToken cancellationToken)
    {
        if (!await handler.HandleAsync(request.Confirm, cancellationToken))
        {
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]> { ["confirm"] = [$"Type {DeleteAccountHandler.ConfirmationWord} to confirm."] }));
        }

        await HttpContext.SignOutAsync(AuthSchemes.Cookie);
        return NoContent();
    }
}
