using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Postings.Fetching;

namespace InternshipCoach.Infrastructure.Web;

/// <summary>
/// Fetches a public web page for "add posting by URL". SSRF defences: every address the host resolves to must be public
/// (checked at connect time, so DNS rebinding cannot swap it), redirects are followed by hand and re-checked, no proxy,
/// default ports only, hard timeout and size cap, HTML responses only.
/// </summary>
public sealed class SafePageFetcher : IPageFetcher
{
    public const int MaxBytes = 1_000_000;
    public const int MaxRedirects = 3;
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    public const string UserAgent = "InternshipCoachBot/1.0 (personal internship tracker; fetches links its user adds)";

    private static readonly Lazy<HttpMessageHandler> DefaultHandler = new(CreateHandler);

    private readonly HttpClient _http;

    public SafePageFetcher() : this(DefaultHandler.Value)
    {
    }

    /// <summary>For tests: supply a fake handler. Address checks at connect time then do not apply, URL checks still do.</summary>
    public SafePageFetcher(HttpMessageHandler handler)
    {
        _http = new HttpClient(handler, disposeHandler: false) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
    }

    public async Task<string> FetchHtmlAsync(Uri url, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        try
        {
            var current = url;
            for (var hop = 0; hop <= MaxRedirects; hop++)
            {
                var problem = PostingUrlPolicy.CheckUri(current);
                if (problem is not null)
                {
                    throw new PageFetchException(problem);
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.UserAgent.ParseAdd(UserAgent);
                request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);

                if (response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
                    or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
                {
                    var location = response.Headers.Location;
                    if (location is null)
                    {
                        throw new PageFetchException("The page redirected without a destination.");
                    }

                    current = location.IsAbsoluteUri ? location : new Uri(current, location);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new PageFetchException($"The site answered with error {(int)response.StatusCode}.");
                }

                return await ReadBodyAsync(response, timeout.Token);
            }

            throw new PageFetchException("The page redirected too many times.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PageFetchException("The page took too long to load.");
        }
        catch (HttpRequestException ex)
        {
            throw new PageFetchException("The page could not be loaded.", ex);
        }
    }

    private static async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is null || !(mediaType.Contains("html", StringComparison.OrdinalIgnoreCase)
                                   || mediaType.Contains("xml", StringComparison.OrdinalIgnoreCase)))
        {
            throw new PageFetchException("That link is not a web page.");
        }

        if (response.Content.Headers.ContentLength > MaxBytes)
        {
            throw new PageFetchException("The page is too large.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                throw new PageFetchException("The page is too large.");
            }

            buffer.Write(chunk, 0, read);
        }

        return Decode(buffer.ToArray(), response.Content.Headers.ContentType);
    }

    private static string Decode(byte[] bytes, MediaTypeHeaderValue? contentType)
    {
        var encoding = Encoding.UTF8;
        if (!string.IsNullOrWhiteSpace(contentType?.CharSet))
        {
            try
            {
                encoding = Encoding.GetEncoding(contentType.CharSet.Trim('"'));
            }
            catch (ArgumentException)
            {
                // Unknown charset: UTF-8 is the best guess.
            }
        }

        return encoding.GetString(bytes);
    }

    private static HttpMessageHandler CreateHandler() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseProxy = false,
        UseCookies = false,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(5),
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        MaxResponseHeadersLength = 32,
        ConnectCallback = ConnectToPublicAddressAsync
    };

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancellationToken);

        // Refuse the whole host when any address is private: a mixed answer is a rebinding signal.
        if (addresses.Length == 0 || !addresses.All(PublicAddress.IsPublic))
        {
            throw new PageFetchException("That address is not public.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
