using InternshipCoach.Application.Abstractions.Llm;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeLlmTransport(params string[] replies) : ILlmTransport
{
    private readonly Queue<string> _replies = new(replies);

    public List<(LlmModel Model, string System, IReadOnlyList<ChatTurn> Turns, int MaxTokens)> Requests { get; } = [];

    public Task<LlmTransportResponse> SendAsync(
        LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct)
    {
        Requests.Add((model, system, turns, maxTokens));
        var text = _replies.Dequeue();
        return Task.FromResult(new LlmTransportResponse(text, 10, 5));
    }
}

public sealed class FakeLlmCallLog : ILlmCallLog
{
    public List<LlmCallRecord> Records { get; } = [];

    public int PreExisting { get; set; }

    public Task AddAsync(LlmCallRecord record, CancellationToken ct)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }

    public Task<int> CountSinceAsync(Guid userId, DateTimeOffset since, CancellationToken ct) =>
        Task.FromResult(PreExisting + Records.Count(r => r.UserId == userId && r.CreatedAt >= since));
}
