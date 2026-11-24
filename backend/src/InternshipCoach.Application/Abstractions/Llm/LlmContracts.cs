namespace InternshipCoach.Application.Abstractions.Llm;

public enum LlmModel
{
    /// <summary>deepseek/deepseek-v4.1-flash-1017. Non-personal text only (postings, language detection).</summary>
    Cheap,

    /// <summary>anthropic/claude-sonnet-5-5. The only model that may see real CV text.</summary>
    Smart
}

public enum ChatRole
{
    User,
    Assistant
}

public sealed record ChatTurn(ChatRole Role, string Content);

/// <summary>Who is calling and for which feature; used for the usage log and the daily cap.</summary>
public sealed record LlmCallContext(Guid UserId, string Feature);

public sealed record LlmText(string Text, int InputTokens, int OutputTokens);

public sealed record LlmResult<T>(T Value, int InputTokens, int OutputTokens, int Attempts);

/// <summary>Daily call cap per user (counted over logged attempts since UTC midnight).</summary>
public sealed record LlmLimits(int DailyCallCap = 200);

public static class LlmModels
{
    public const string CheapId = "deepseek/deepseek-v4.1-flash-1017";
    public const string SmartId = "anthropic/claude-sonnet-5-5";
    public const int SmartMinMaxTokens = 4000;

    public static string Id(LlmModel model) => model switch
    {
        LlmModel.Cheap => CheapId,
        LlmModel.Smart => SmartId,
        _ => throw new ArgumentOutOfRangeException(nameof(model))
    };
}

public interface ILlmClient
{
    /// <summary>Returns JSON validated against <paramref name="jsonSchema"/>. At most 2 attempts.</summary>
    Task<LlmResult<T>> CompleteJsonAsync<T>(
        LlmCallContext context, LlmModel model, string system, string user, string jsonSchema, int maxTokens, CancellationToken ct);

    /// <summary>Plain text reply. Retries once on an empty reply.</summary>
    Task<LlmText> CompleteTextAsync(
        LlmCallContext context, LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct);
}

/// <summary>Raw call to the gateway. One request, no retries, no validation.</summary>
public interface ILlmTransport
{
    Task<LlmTransportResponse> SendAsync(
        LlmModel model, string system, IReadOnlyList<ChatTurn> turns, int maxTokens, CancellationToken ct);
}

public sealed record LlmTransportResponse(string Text, int InputTokens, int OutputTokens);

public sealed record LlmCallRecord(
    Guid UserId, string Feature, string Model, int InputTokens, int OutputTokens, int DurationMs, bool Success, DateTimeOffset CreatedAt);

public interface ILlmCallLog
{
    Task AddAsync(LlmCallRecord record, CancellationToken ct);

    Task<int> CountSinceAsync(Guid userId, DateTimeOffset since, CancellationToken ct);
}

public class LlmException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The gateway could not be reached, rejected the request, or is not configured.</summary>
public sealed class LlmUnavailableException(string message, Exception? inner = null) : LlmException(message, inner);

public sealed class LlmDailyCapExceededException(int cap)
    : LlmException($"Daily AI call limit of {cap} reached. Try again tomorrow.");

/// <summary>The model never produced valid output within the attempt cap.</summary>
public sealed class LlmInvalidResponseException(string message) : LlmException(message);
