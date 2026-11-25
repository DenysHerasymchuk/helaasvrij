using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using InternshipCoach.Application.Sources;
using InternshipCoach.Application.Sources.Site;

namespace InternshipCoach.Infrastructure.Sources;

public sealed partial class WebPageFetcher : IWebPageFetcher
{
    private const int MaxBytes = 1024 * 1024;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;

    public WebPageFetcher()
    {
        // Every connection (including redirects) re-checks the resolved address, so a DNS answer that points
        // at a private network is refused even when the host name looked harmless.
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            MaxAutomaticRedirections = 3,
            ConnectTimeout = Timeout,
            ConnectCallback = ConnectToPublicAddressAsync
        };
        _http = new HttpClient(handler) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("InternshipCoach/1.0");
    }

    public async Task<string> FetchTextAsync(Uri url, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);

        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new SourceFetchException($"The site answered with an error ({(int)response.StatusCode}).");
            }

            // A redirect may have landed on a site we never read.
            try
            {
                SiteUrlPolicy.Validate(response.RequestMessage?.RequestUri?.ToString());
            }
            catch (SourceInputException)
            {
                throw new SourceFetchException("The site redirected to an address that is not allowed.");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType ?? "text/html";
            if (mediaType is not ("text/html" or "application/xhtml+xml" or "text/plain"))
            {
                throw new SourceFetchException("The address is not a web page.");
            }

            var raw = await ReadLimitedAsync(response, timeout.Token);
            return mediaType == "text/plain" ? raw : HtmlToText(raw);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new SourceFetchException("Could not load the site (blocked, unreachable or too slow).");
        }
    }

    public static string HtmlToText(string html)
    {
        var document = new HtmlParser().ParseDocument(html);
        foreach (var element in document.QuerySelectorAll("script, style, noscript, svg, template, head").ToList())
        {
            element.Remove();
        }

        foreach (var element in document.QuerySelectorAll("br, p, div, li, h1, h2, h3, h4, h5, h6, tr, section, article, header, footer").ToList())
        {
            element.After(document.CreateTextNode("\n"));
        }

        var text = document.Body?.TextContent ?? string.Empty;
        text = HorizontalSpace().Replace(text, " ");
        text = BlankLines().Replace(text, "\n");
        return text.Trim();
    }

    // Stops reading at 1 MB; a longer page is cut, not rejected.
    private static async Task<string> ReadLimitedAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[MaxBytes];
        var total = 0;
        while (total < MaxBytes)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, MaxBytes - total), ct);
            if (read == 0)
            {
                break;
            }

            total += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, total);
    }

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, ct);

        if (addresses.Length == 0 || addresses.Any(address => !SiteUrlPolicy.IsPublic(address)))
        {
            throw new HttpRequestException("Address is not allowed.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, ct);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    [GeneratedRegex(@"[ \t ]+")]
    private static partial Regex HorizontalSpace();

    [GeneratedRegex(@"\s*\n\s*")]
    private static partial Regex BlankLines();
}
