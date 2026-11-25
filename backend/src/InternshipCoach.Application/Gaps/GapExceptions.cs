namespace InternshipCoach.Application.Gaps;

/// <summary>One re-scan per requirement per minute. Maps to HTTP 429.</summary>
public sealed class RescanThrottledException(int retryAfterSeconds)
    : Exception($"This gap was re-scanned a moment ago. Try again in {retryAfterSeconds} seconds.")
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
