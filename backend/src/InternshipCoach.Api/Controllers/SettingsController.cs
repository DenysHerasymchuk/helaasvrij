using InternshipCoach.Application.Settings;
using InternshipCoach.Application.Settings.Get;
using InternshipCoach.Application.Settings.Update;
using InternshipCoach.Application.Writing;
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
            new UpdateSettingsCommand(request.DefaultLanguage, request.StrongMatchThreshold, request.EmailAlerts, request.DocumentSkills, request.InterviewSkills),
            cancellationToken);

        if (!result.Succeeded)
        {
            return ValidationProblem(new ValidationProblemDetails(
                result.Errors!.ToDictionary(pair => pair.Key, pair => new[] { pair.Value })));
        }

        return Ok(ToResponse(result));
    }

    /// <summary>The writing skills a user can switch on per area.</summary>
    [HttpGet("writing-skills")]
    public IReadOnlyList<WritingSkillResponse> WritingSkillCatalog() =>
        WritingSkills.All.Select(s => new WritingSkillResponse(s.Id, s.Name, s.Description)).ToList();

    private static SettingsResponse ToResponse(SettingsResult result) =>
        new(result.DefaultLanguage, result.StrongMatchThreshold, result.EmailAlerts, result.DocumentSkills ?? [], result.InterviewSkills ?? []);
}
