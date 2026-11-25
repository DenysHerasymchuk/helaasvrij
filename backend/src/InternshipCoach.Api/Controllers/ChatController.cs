using InternshipCoach.Api.Chat;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Chat.Confirm;
using InternshipCoach.Application.Chat.Send;
using InternshipCoach.Application.Chat.Tools;
using InternshipCoach.Contracts.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InternshipCoach.Api.Controllers;

[Route("api/chat")]
[ApiController]
[Authorize]
public sealed class ChatController : ControllerBase
{
    [HttpPost]
    [Consumes("application/json")]
    [EnableRateLimiting(ChatRateLimiting.PolicyName)]
    public async Task<IActionResult> Send(ChatRequest request, [FromServices] SendChatHandler handler, CancellationToken cancellationToken)
    {
        var turns = (request.Messages ?? [])
            .Select(m => new ChatTurn(
                string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant : ChatRole.User,
                m.Content ?? string.Empty))
            .ToList();

        try
        {
            var result = await handler.HandleAsync(new SendChatCommand(turns, request.Route), cancellationToken);
            if (result.Status != SendChatStatus.Ok)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
            }

            var trace = (result.ToolTrace ?? [])
                .Select(t => new ChatToolTraceDto(t.Name, t.Args, t.Ok ? "ok" : "error", t.Summary, t.DurationMs))
                .ToList();
            var pending = result.PendingAction is { } p ? new ChatPendingActionDto(p.Tool, p.Args, p.Summary) : null;
            return Ok(new ChatResponse(result.Reply!, result.NavigateTo, trace, pending));
        }
        catch (LlmDailyCapExceededException ex)
        {
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: ex.Message);
        }
        catch (LlmException)
        {
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "The assistant is unavailable right now. Try again.");
        }
    }

    [HttpPost("actions/confirm")]
    [Consumes("application/json")]
    [EnableRateLimiting(ChatRateLimiting.PolicyName)]
    public async Task<IActionResult> Confirm(
        ConfirmChatActionRequest request, [FromServices] ConfirmChatActionHandler handler, CancellationToken cancellationToken)
    {
        try
        {
            var result = await handler.HandleAsync(new ConfirmChatActionCommand(request.Tool, request.Args), cancellationToken);
            return result.Status switch
            {
                ChatActionStatus.Ok => Ok(new ConfirmChatActionResponse(result.Message, result.NavigateTo,
                    result.Undo is { } u ? new ChatPendingActionDto(u.Tool, u.Args, u.Summary) : null)),
                ChatActionStatus.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: result.Message),
                _ => Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Message)
            };
        }
        catch (LlmDailyCapExceededException ex)
        {
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: ex.Message);
        }
        catch (LlmException)
        {
            // save_job reads the ad with the model before saving it.
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "Could not save this job right now. Try again.");
        }
    }
}
