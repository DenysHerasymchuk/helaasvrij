using System.Text.Json;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;

namespace InternshipCoach.Infrastructure.Llm.Replay;

/// <summary>
/// Demo-mode decorator over the real <see cref="ILlmClient"/>. A recorded call is answered from the store without
/// the network; a miss goes to the live client and is stored when recording is on. Never logs prompts or replies.
/// </summary>
public sealed class ReplayLlmClient(ILlmClient inner, ReplayStore store, DemoReplayOptions options) : ILlmClient
{
    public async Task<LlmResult<T>> CompleteJsonAsync<T>(
        LlmCallContext context, LlmModel model, string system, string user, string jsonSchema, int maxTokens, CancellationToken ct)
    {
        var key = ReplayKey.Compute(model, system, user);
        if (store.TryGet(key, out var hit) && hit.Kind == "json")
        {
            var value = JsonSerializer.Deserialize<T>(hit.Payload, LlmJson.SerializerOptions);
            if (value is not null)
            {
                return new LlmResult<T>(value, hit.InputTokens, hit.OutputTokens, 1);
            }
        }

        var result = await inner.CompleteJsonAsync<T>(context, model, system, user, jsonSchema, maxTokens, ct);
        if (options.Record)
        {
            store.Save(key, new ReplayEntry("json", LlmModels.Id(model),
                JsonSerializer.Serialize(result.Value, LlmJson.SerializerOptions), result.InputTokens, result.OutputTokens));
        }

        return result;
    }

    public async Task<LlmText> CompleteTextAsync(
        LlmCallContext context, LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct)
    {
        var key = ReplayKey.Compute(model, system, ReplayKey.Flatten(turns));
        if (store.TryGet(key, out var hit) && hit.Kind == "text")
        {
            return new LlmText(hit.Payload, hit.InputTokens, hit.OutputTokens);
        }

        var result = await inner.CompleteTextAsync(context, model, system, turns, maxTokens, ct);
        if (options.Record)
        {
            store.Save(key, new ReplayEntry("text", LlmModels.Id(model), result.Text, result.InputTokens, result.OutputTokens));
        }

        return result;
    }
}
