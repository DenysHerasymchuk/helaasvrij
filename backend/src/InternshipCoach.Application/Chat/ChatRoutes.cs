using System.Text.RegularExpressions;

namespace InternshipCoach.Application.Chat;

/// <summary>The only pages the assistant may point to, and the parser for its NAVIGATE hint.</summary>
public static partial class ChatRoutes
{
    public static readonly IReadOnlyList<(string Name, string Route)> Pages =
    [
        ("Dashboard", "/dashboard"),
        ("Postings", "/postings"),
        ("Gap plan", "/gaps"),
        ("Documents", "/documents"),
        ("Interview prep", "/interview"),
        ("Applications", "/applications"),
        ("Profile", "/profile"),
        ("Settings", "/settings")
    ];

    public static bool IsAllowed(string? route) => Normalize(route) is not null;

    /// <summary>Returns the allowed route in canonical form, or null when it is not on the whitelist.</summary>
    public static string? Normalize(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return null;
        }

        var trimmed = route.Trim();
        if (trimmed.Length > 1)
        {
            trimmed = trimmed.TrimEnd('/');
        }

        foreach (var (_, allowed) in Pages)
        {
            if (string.Equals(allowed, trimmed, StringComparison.Ordinal))
            {
                return allowed;
            }
        }

        return null;
    }

    /// <summary>
    /// Removes every <c>NAVIGATE:</c> line from the reply and returns the last one's route when it is whitelisted.
    /// Unknown or malicious targets (for example a URL) are stripped and ignored.
    /// </summary>
    public static (string Reply, string? NavigateTo) ExtractNavigation(string reply)
    {
        string? target = null;
        var kept = new List<string>();

        foreach (var line in reply.Replace("\r\n", "\n").Split('\n'))
        {
            var match = NavigateLine().Match(line);
            if (match.Success)
            {
                target = Normalize(match.Groups[1].Value);
                continue;
            }

            kept.Add(line);
        }

        return (string.Join('\n', kept).Trim(), target);
    }

    [GeneratedRegex(@"^\s*NAVIGATE\s*:\s*(.*?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex NavigateLine();
}
