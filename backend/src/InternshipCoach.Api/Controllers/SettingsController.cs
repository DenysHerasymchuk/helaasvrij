using InternshipCoach.Application.Settings;
using InternshipCoach.Application.Settings.Get;
using InternshipCoach.Application.Settings.Update;
using InternshipCoach.Contracts.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/settings")]
[ApiController]
[Authorize]
public sealed class SettingsController : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SettingsResponse>> Get([FromServices] GetSettingsHandler handler, CancellationToken cancellationToken) =>
        Ok(ToResponse(await handler.HandleAsync(cancellationToken)));

    [HttpPut]
    [Consumes("application/json")]
    public async Task<IActionResult> Put(
        UpdateSettingsRequest request, [FromServices] UpdateSettingsHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new UpdateSettingsCommand(request.DefaultLanguage, request.StrongMatchThreshold, request.EmailAlerts), cancellationToken);

        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(
                result.Errors!.ToDictionary(pair => pair.Key, pair => new[] { pair.Value })));
        }

        return Ok(ToResponse(result));
    }

    private static SettingsResponse ToResponse(SettingsResult result) =>
        new(result.DefaultLanguage, result.StrongMatchThreshold, result.EmailAlerts);
}
