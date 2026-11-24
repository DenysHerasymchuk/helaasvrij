using InternshipCoach.Application.Abstractions.Llm;
using Microsoft.Extensions.Logging;

namespace InternshipCoach.Infrastructure.Llm;

// route33 first; when it is unavailable (no key, 401/403, 429, 5xx, timeout, network) ask the Claude CLI.
// Logs only which transport answered, never content.
public sealed class FallbackLlmTransport(ILlmTransport primary, ILlmTransport fallback, ILogger<FallbackLlmTransport> logger) : ILlmTransport
{
    public async Task<LlmTransportResponse> SendAsync(
        LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct)
    {
        try
        {
            var response = await primary.SendAsync(model, system, turns, maxTokens, ct);
            logger.LogDebug("LLM {Model} answered by route33", model);
            return response;
        }
        catch (LlmUnavailableException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("route33 unavailable ({Reason}); falling back to Claude CLI", ex.Message);
        }

        var fallbackResponse = await fallback.SendAsync(model, system, turns, maxTokens, ct);
        logger.LogInformation("LLM {Model} answered by Claude CLI fallback", model);
        return fallbackResponse;
    }
}
