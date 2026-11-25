namespace InternshipCoach.Domain.Postings;

/// <summary>How well the student's evidence covers one requirement. The model labels it; code turns it into a number.</summary>
public enum Coverage
{
    None,
    Partial,
    Strong
}

public static class ScoreWeights
{
    /// <summary>Required counts double.</summary>
    public static int Weight(this Importance importance) => importance == Importance.Required ? 2 : 1;

    /// <summary>Coverage in half units: none 0, partial 1, strong 2 (so 0, 0.5 and 1 without floating point).</summary>
    public static int HalfUnits(this Coverage coverage) => coverage switch
    {
        Coverage.Strong => 2,
        Coverage.Partial => 1,
        _ => 0
    };

    public static bool TryParse(string? value, out Coverage coverage)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "strong":
                coverage = Coverage.Strong;
                return true;
            case "partial":
                coverage = Coverage.Partial;
                return true;
            case "none":
                coverage = Coverage.None;
                return true;
            default:
                coverage = Coverage.None;
                return false;
        }
    }
}
