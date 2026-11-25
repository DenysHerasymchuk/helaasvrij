using System.Text.Json;
using InternshipCoach.Api.Chat;
using InternshipCoach.Application.Chat.Tools;
using InternshipCoach.Contracts.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InternshipCoach.Api.Controllers;

/// <summary>
/// The assistant's tool catalog over HTTP, for the MCP server. Read tools run; action tools only return a proposal,
/// and the change happens through POST /api/chat/actions/confirm, the same confirm step the chat uses.
/// </summary>
[Route("api/tools")]
[ApiController]
[Authorize]
public sealed class ToolsController : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<ToolDto> Catalog() =>
        ChatToolbox.ReadTools.Concat(ChatToolbox.ActionTools)
            .Select(t => new ToolDto(
                t.Name, t.Description, t.IsAction ? "action" : "read",
                t.Params.Select(p => new ToolParamDto(p.Name, p.Description, p.Required, p.Values)).ToList()))
            .ToList();

    [HttpPost("{name}/run")]
    [Consumes("application/json")]
    [EnableRateLimiting(ChatRateLimiting.PolicyName)]
    public async Task<IActionResult> Run(string name, RunToolRequest request, [FromServices] ChatToolbox tools, CancellationToken cancellationToken)
    {
        var args = (request.Args ?? new Dictionary<string, string>())
            .Where(pair => pair.Value is not null)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Trim(), StringComparer.Ordinal);

        try
        {
            if (ChatToolbox.IsActionTool(name))
            {
                var p = await tools.ProposeAsync(name, args, cancellationToken);
                return Ok(new RunToolResponse("proposal", null, null, new ChatPendingActionDto(p.Tool, p.Args, p.Summary)));
            }

            if (!ChatToolbox.IsReadTool(name))
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Unknown tool.");
            }

            var output = await tools.RunReadAsync(name, args, cancellationToken);
            return Ok(new RunToolResponse("read", JsonSerializer.SerializeToElement(output.Data, JsonSerializerOptions.Web), output.Summary, null));
        }
        catch (ChatToolException ex)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: ex.Message);
        }
    }
}
