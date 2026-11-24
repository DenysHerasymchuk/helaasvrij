using System.Text;

namespace InternshipCoach.Application.Llm;

public static class QuoteCheck
{
    /// <summary>True only if the quote is an exact substring of the source, ignoring differences in whitespace.</summary>
    public static bool IsVerbatim(string? quote, string? source)
    {
        if (string.IsNullOrWhiteSpace(quote) || string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        return Normalise(source).Contains(Normalise(quote), StringComparison.Ordinal);
    }

    private static string Normalise(string text)
    {
        var builder = new StringBuilder(text.Length);
        var pendingSpace = false;

        foreach (var c in text.Trim())
        {
            if (char.IsWhiteSpace(c))
            {
                pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(c);
        }

        return builder.ToString();
    }
}
