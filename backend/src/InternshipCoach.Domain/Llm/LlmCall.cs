using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Llm;

// One row per attempt sent to the LLM gateway. Stores counts and timing only, never prompt or response text.
public sealed class LlmCall : IUserOwned
{
    public const int FeatureMaxLength = 100;
    public const int ModelMaxLength = 200;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Feature { get; private set; } = null!;
    public string Model { get; private set; } = null!;
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public int DurationMs { get; private set; }
    public bool Success { get; private set; }
    public CreatedAt CreatedAt { get; private set; } = null!;

    private LlmCall()
    {
    }

    public static LlmCall Create(
        Guid userId, string feature, string model, int inputTokens, int outputTokens, int durationMs, bool success, CreatedAt createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        if (string.IsNullOrWhiteSpace(feature))
        {
            throw new DomainException("Feature is required.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new DomainException("Model is required.");
        }

        if (inputTokens < 0 || outputTokens < 0 || durationMs < 0)
        {
            throw new DomainException("Token counts and duration cannot be negative.");
        }

        return new LlmCall
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Feature = Truncate(feature.Trim(), FeatureMaxLength),
            Model = Truncate(model.Trim(), ModelMaxLength),
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            DurationMs = durationMs,
            Success = success,
            CreatedAt = createdAt ?? throw new DomainException("Creation time is required.")
        };
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}
