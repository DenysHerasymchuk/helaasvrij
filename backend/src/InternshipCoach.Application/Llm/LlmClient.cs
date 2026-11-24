using System.Diagnostics;
using System.Text.Json;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;

namespace InternshipCoach.Application.Llm;

public sealed class LlmClient(ILlmTransport transport, ILlmCallLog log, IClock clock, LlmLimits limits) : ILlmClient
{
    public const int MaxAttempts = 2;

    public async Task<LlmResult<T>> CompleteJsonAsync<T>(
        LlmCallContext context, LlmModel model, string system, string user, string jsonSchema, int maxTokens, CancellationToken ct)
    {
        await EnforceDailyCapAsync(context, ct);

        var fullSystem = $"{system}\n\n{UntrustedInput.SystemNotice}\n\n" +
                         "Reply with one JSON value only, no prose and no code fence. It must validate against this JSON Schema:\n" +
                         jsonSchema;
        var turns = new List<ChatTurn> { new(ChatRole.User, user) };
        string? lastError = null;
        int totalIn = 0, totalOut = 0;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var response = await SendAndLogAsync(context, model, fullSystem, turns, maxTokens, ct,
                text => Check<T>(text, jsonSchema));
            totalIn += response.Response.InputTokens;
            totalOut += response.Response.OutputTokens;

            if (response.Error is null)
            {
                return new LlmResult<T>(response.Value!, totalIn, totalOut, attempt);
            }

            lastError = response.Error;
            turns =
            [
                new ChatTurn(ChatRole.User, user),
                new ChatTurn(ChatRole.Assistant, string.IsNullOrWhiteSpace(response.Response.Text) ? "(empty)" : response.Response.Text),
                new ChatTurn(ChatRole.User, $"That reply was rejected: {response.Error} Reply again with only valid JSON that matches the schema.")
            ];
        }

        throw new LlmInvalidResponseException($"The model did not return valid JSON after {MaxAttempts} attempts: {lastError}");
    }

    public async Task<LlmText> CompleteTextAsync(
        LlmCallContext context, LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct)
    {
        await EnforceDailyCapAsync(context, ct);

        var fullSystem = $"{system}\n\n{UntrustedInput.SystemNotice}";
        int totalIn = 0, totalOut = 0;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var result = await SendAndLogAsync<string>(context, model, fullSystem, turns, maxTokens, ct,
                text => string.IsNullOrWhiteSpace(text) ? (null, "The reply was empty.") : (text.Trim(), null));
            totalIn += result.Response.InputTokens;
            totalOut += result.Response.OutputTokens;

            if (result.Error is null)
            {
                return new LlmText(result.Value!, totalIn, totalOut);
            }
        }

        throw new LlmInvalidResponseException($"The model returned an empty reply after {MaxAttempts} attempts.");
    }

    private static (T? Value, string? Error) Check<T>(string text, string jsonSchema)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (default, "The reply was empty.");
        }

        var json = LlmJson.ExtractJson(text);
        var problem = LlmJson.Validate(json, jsonSchema);
        if (problem is not null)
        {
            return (default, problem);
        }

        try
        {
            var value = JsonSerializer.Deserialize<T>(json, LlmJson.SerializerOptions);
            return value is null ? (default, "The reply was null.") : (value, null);
        }
        catch (JsonException ex)
        {
            return (default, $"Could not read the JSON: {ex.Message}");
        }
    }

    private async Task EnforceDailyCapAsync(LlmCallContext context, CancellationToken ct)
    {
        var startOfDay = new DateTimeOffset(clock.UtcNow.UtcDateTime.Date, TimeSpan.Zero);
        var used = await log.CountSinceAsync(context.UserId, startOfDay, ct);
        if (used >= limits.DailyCallCap)
        {
            throw new LlmDailyCapExceededException(limits.DailyCallCap);
        }
    }

    private async Task<Attempt<T>> SendAndLogAsync<T>(
        LlmCallContext context, LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct,
        Func<string, (T? Value, string? Error)> check)
    {
        var effectiveMax = model == LlmModel.Smart ? Math.Max(maxTokens, LlmModels.SmartMinMaxTokens) : maxTokens;
        var stopwatch = Stopwatch.StartNew();
        LlmTransportResponse response;

        try
        {
            response = await transport.SendAsync(model, system, turns, effectiveMax, ct);
        }
        catch (LlmException) when (!ct.IsCancellationRequested)
        {
            await WriteLogAsync(context, model, 0, 0, stopwatch, false);
            throw;
        }

        var (value, error) = check(response.Text);
        await WriteLogAsync(context, model, response.InputTokens, response.OutputTokens, stopwatch, error is null);
        return new Attempt<T>(response, value, error);
    }

    // The log write must survive a cancelled request, so it does not take the request token.
    private Task WriteLogAsync(LlmCallContext context, LlmModel model, int input, int output, Stopwatch stopwatch, bool success) =>
        log.AddAsync(
            new LlmCallRecord(context.UserId, context.Feature, LlmModels.Id(model), input, output,
                (int)stopwatch.ElapsedMilliseconds, success, clock.UtcNow),
            CancellationToken.None);

    private sealed record Attempt<T>(LlmTransportResponse Response, T? Value, string? Error);
}
