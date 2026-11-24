using System.Net;
using System.Net.Sockets;

namespace InternshipCoach.Application.Sources.Site;

/// <summary>Decides which URLs the server may fetch (SSRF guard) and which sites we never read.</summary>
public static class SiteUrlPolicy
{
    private static readonly string[] BlockedDomains = ["linkedin.com", "student.be", "jobteaser.com"];

    /// <summary>Returns the parsed URL or throws <see cref="SourceInputException" />.</summary>
    public static Uri Validate(string? input)
    {
        if (!Uri.TryCreate(input?.Trim(), UriKind.Absolute, out var url)
            || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            throw new SourceInputException("Enter a full web address starting with http:// or https://.");
        }

        if (!string.IsNullOrEmpty(url.UserInfo))
        {
            throw new SourceInputException("Web addresses with a user name or password are not allowed.");
        }

        var host = url.IdnHost.TrimEnd('.').ToLowerInvariant();
        if (BlockedDomains.Any(domain => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal)))
        {
            throw new SourceInputException("This site cannot be added. Use your own exported PDF or paste the text.");
        }

        if (host == "localhost" || host.EndsWith(".localhost", StringComparison.Ordinal)
            || host.EndsWith(".local", StringComparison.Ordinal) || host.EndsWith(".internal", StringComparison.Ordinal))
        {
            throw new SourceInputException("This address is not allowed.");
        }

        if (IPAddress.TryParse(host.Trim('[', ']'), out var literal) && !IsPublic(literal))
        {
            throw new SourceInputException("This address is not allowed.");
        }

        return url;
    }

    /// <summary>True only for addresses on the public internet.</summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(bytes[0] == 10
                || bytes[0] == 127
                || bytes[0] == 0
                || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) // carrier-grade NAT
                || (bytes[0] == 169 && bytes[1] == 254) // link-local, cloud metadata
                || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 0)
                || (bytes[0] == 198 && bytes[1] is 18 or 19)
                || bytes[0] >= 224); // multicast and reserved
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || (bytes[0] & 0xFE) == 0xFC); // unique local fc00::/7
        }

        return false;
    }
}
