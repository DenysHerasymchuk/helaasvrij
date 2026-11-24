using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Sources;

namespace InternshipCoach.Application.Sources.Site;

public sealed class AddSiteHandler(ISourceRepository sources, IWebPageFetcher fetcher, ICurrentUser currentUser, IClock clock)
{
    public const int MaxTextLength = 50_000;

    public async Task<SourceSummary> HandleAsync(AddSiteCommand command, CancellationToken ct)
    {
        var url = SiteUrlPolicy.Validate(command.Url);
        var source = Source.Create(currentUser.UserId, SourceKind.Site, url.Host);

        try
        {
            var text = (await fetcher.FetchTextAsync(url, ct)).Trim();
            if (text.Length == 0)
            {
                throw new SourceFetchException("The page has no readable text.");
            }

            source.AddDocument(url.ToString(), text.Length > MaxTextLength ? text[..MaxTextLength] : text);
            source.MarkReady(clock.UtcNow);
        }
        catch (SourceFetchException ex)
        {
            source.MarkFailed(ex.Message, clock.UtcNow);
        }

        await sources.AddAsync(source, ct);
        return SourceSummary.From(source);
    }
}
