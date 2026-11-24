using System.Net;
using System.Net.Sockets;

namespace InternshipCoach.Application.Postings.Fetching;

/// <summary>SSRF guard: true only for addresses that are routable on the public internet.</summary>
public static class PublicAddress
{
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
            return IsPublicV4(bytes);
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return false;
        }

        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6UniqueLocal)
        {
            return false;
        }

        // 2001:db8::/32 documentation
        if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x0d && bytes[3] == 0xb8)
        {
            return false;
        }

        // 64:ff9b::/96 NAT64 embeds an IPv4 address in the last 4 bytes
        if (bytes[0] == 0x00 && bytes[1] == 0x64 && bytes[2] == 0xff && bytes[3] == 0x9b)
        {
            return IsPublicV4(bytes[12..16]);
        }

        // 2002::/16 (6to4) embeds an IPv4 address in bytes 2..5; 2001::/32 (Teredo) is not worth allowing
        if (bytes[0] == 0x20 && bytes[1] == 0x02)
        {
            return IsPublicV4(bytes[2..6]);
        }

        if (bytes[0] == 0x20 && bytes[1] == 0x01 && bytes[2] == 0x00 && bytes[3] == 0x00)
        {
            return false;
        }

        // ::/96 IPv4-compatible and anything else in the unspecified block
        return !bytes[..12].All(b => b == 0);
    }

    private static bool IsPublicV4(byte[] b) =>
        !(b[0] == 0                                          // 0.0.0.0/8
          || b[0] == 10                                      // 10.0.0.0/8
          || b[0] == 127                                     // loopback
          || (b[0] == 100 && b[1] is >= 64 and <= 127)       // 100.64.0.0/10 carrier-grade NAT
          || (b[0] == 169 && b[1] == 254)                    // link-local, cloud metadata
          || (b[0] == 172 && b[1] is >= 16 and <= 31)        // 172.16.0.0/12
          || (b[0] == 192 && b[1] == 0 && b[2] == 0)         // 192.0.0.0/24
          || (b[0] == 192 && b[1] == 0 && b[2] == 2)         // documentation
          || (b[0] == 192 && b[1] == 168)                    // 192.168.0.0/16
          || (b[0] == 198 && b[1] is 18 or 19)               // benchmarking
          || (b[0] == 198 && b[1] == 51 && b[2] == 100)      // documentation
          || (b[0] == 203 && b[1] == 0 && b[2] == 113)       // documentation
          || b[0] >= 224);                                   // multicast, reserved, broadcast
}
