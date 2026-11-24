namespace InternshipCoach.Application.Sources;

public interface IWebPageFetcher
{
    /// <summary>Downloads a page (5 s, 1 MB cap, public addresses only) and returns its visible text.</summary>
    Task<string> FetchTextAsync(Uri url, CancellationToken ct);
}
