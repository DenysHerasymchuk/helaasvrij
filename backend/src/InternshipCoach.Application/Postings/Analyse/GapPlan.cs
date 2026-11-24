using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Analyse;

public static class GapPlan
{
    public const int FirstDay = 3;
    public const int LastDay = 21;

    /// <summary>
    /// Due dates for the open gaps, spread over the next three weeks with the biggest impact (weight x missing
    /// coverage) first. Strong requirements get none. A single gap is due in a week.
    /// </summary>
    public static IReadOnlyDictionary<Guid, DateOnly> DueDates(IEnumerable<(Requirement Requirement, Coverage Coverage)> items, DateOnly today)
    {
        var gaps = items
            .Where(item => item.Coverage != Coverage.Strong)
            .OrderByDescending(item => item.Requirement.Importance.Weight() * (2 - item.Coverage.HalfUnits()))
            .ThenBy(item => item.Requirement.Position)
            .ToList();

        var result = new Dictionary<Guid, DateOnly>();
        for (var i = 0; i < gaps.Count; i++)
        {
            var day = gaps.Count == 1
                ? 7
                : FirstDay + (int)Math.Round(i * (LastDay - FirstDay) / (double)(gaps.Count - 1), MidpointRounding.AwayFromZero);
            result[gaps[i].Requirement.Id] = today.AddDays(day);
        }

        return result;
    }
}
