using InternshipCoach.Api.Chat;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Chat.Send;
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
            return result.Status == SendChatStatus.Ok
                ? Ok(new ChatResponse(result.Reply!, result.NavigateTo))
                : Problem(statusCode: StatusCodes.Status400BadRequest, title: result.Error);
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
}
