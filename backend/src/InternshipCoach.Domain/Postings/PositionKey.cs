namespace InternshipCoach.Domain.Postings;

public static class PositionKey
{
    /// <summary>"BRI-12": the first three letters of the company and the posting number.</summary>
    public static string Of(string company, int number)
    {
        var letters = new string(company.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        return $"{(letters.Length == 0 ? "POS" : letters)}-{number}";
    }
}
