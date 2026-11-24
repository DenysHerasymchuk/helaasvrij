namespace InternshipCoach.Application.Abstractions;

/// <summary>Downloads one public web page. Implementations must block private addresses and cap time and size.</summary>
public interface IPageFetcher
{
    /// <exception cref="PageFetchException">The page could not be fetched safely.</exception>
    Task<string> FetchHtmlAsync(Uri url, CancellationToken cancellationToken);
}

public sealed class PageFetchException(string message, Exception? inner = null) : Exception(message, inner);
