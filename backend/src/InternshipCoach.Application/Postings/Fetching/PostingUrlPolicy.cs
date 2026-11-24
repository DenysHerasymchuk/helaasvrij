using System.Net;

namespace InternshipCoach.Application.Postings.Fetching;

/// <summary>Which URLs the app may fetch for "add posting by URL". Pure checks, no network.</summary>
public static class PostingUrlPolicy
{
    public const int MaxUrlLength = 2000;

    // Never fetched: their terms forbid scraping (see CLAUDE.md).
    private static readonly string[] BlockedDomains = ["linkedin.com", "lnkd.in", "student.be", "jobteaser.com"];

    private static readonly string[] InternalSuffixes = [".localhost", ".local", ".internal", ".lan", ".home.arpa", ".intranet"];

    /// <summary>Returns the parsed URL, or null with a message for the user.</summary>
    public static (Uri? Uri, string? Error) Check(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return (null, "Enter the link of the posting.");
        }

        if (text.Length > MaxUrlLength || !Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return (null, "That is not a valid link. Use a full address starting with https://.");
        }

        var error = CheckUri(uri);
        return error is null ? (uri, null) : (null, error);
    }

    /// <summary>Also used for every redirect hop. Returns null when the URL is allowed.</summary>
    public static string? CheckUri(Uri uri)
    {
        if (uri.Scheme is not ("http" or "https"))
        {
            return "Only http and https links are supported.";
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return "Links with a user name or password are not supported.";
        }

        if (uri.Port != (uri.Scheme == "https" ? 443 : 80))
        {
            return "Links with a custom port are not supported.";
        }

        var host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (host.Length == 0)
        {
            return "That is not a valid link.";
        }

        if (BlockedDomains.Any(domain => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal)))
        {
            return "LinkedIn, Student.be and JobTeaser links are not supported. Paste the posting text instead.";
        }

        if (IPAddress.TryParse(host.Trim('[', ']'), out var address))
        {
            return PublicAddress.IsPublic(address) ? null : "That address is not public.";
        }

        if (!host.Contains('.') || host == "localhost" || InternalSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.Ordinal)))
        {
            return "That address is not public.";
        }

        return null;
    }
}
