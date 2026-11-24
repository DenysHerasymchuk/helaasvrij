using System.Collections.Concurrent;

namespace InternshipCoach.Application.Gaps;

/// <summary>
/// Allows one re-scan per requirement per minute. Kept in memory (a restart only resets the cost guard), so no
/// schema change is needed. Register as a singleton.
/// </summary>
public sealed class RescanThrottle
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentDictionary<(Guid UserId, Guid RequirementId), DateTimeOffset> _last = new();

    /// <summary>Records the attempt and returns 0, or returns the seconds left to wait without recording.</summary>
    public int TryAcquire(Guid userId, Guid requirementId, DateTimeOffset now)
    {
        var key = (userId, requirementId);
        var wait = 0;
        _last.AddOrUpdate(
            key,
            _ => now,
            (_, last) =>
            {
                var left = Window - (now - last);
                if (left > TimeSpan.Zero)
                {
                    wait = (int)Math.Ceiling(left.TotalSeconds);
                    return last;
                }

                return now;
            });

        // Drop old entries so the map cannot grow without bound.
        if (_last.Count > 1000)
        {
            foreach (var pair in _last.Where(p => now - p.Value > Window))
            {
                _last.TryRemove(pair.Key, out _);
            }
        }

        return wait;
    }
}
