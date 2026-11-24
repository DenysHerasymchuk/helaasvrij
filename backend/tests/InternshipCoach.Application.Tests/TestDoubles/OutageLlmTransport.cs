using InternshipCoach.Application.Abstractions.Llm;

namespace InternshipCoach.Application.Tests.TestDoubles;

/// <summary>Answers the given replies, then simulates an outage: throws LlmUnavailableException, or hangs until cancelled.</summary>
public sealed class OutageLlmTransport(bool hang, params string[] replies) : ILlmTransport
{
    private readonly Queue<string> _replies = new(replies);

    public int Calls { get; private set; }

    public async Task<LlmTransportResponse> SendAsync(
        LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct)
    {
        Calls++;
        if (_replies.TryDequeue(out var text))
        {
            return new LlmTransportResponse(text, 10, 5);
        }

        if (hang)
        {
            await Task.Delay(Timeout.Infinite, ct);
        }

        throw new LlmUnavailableException("gateway down");
    }
}
