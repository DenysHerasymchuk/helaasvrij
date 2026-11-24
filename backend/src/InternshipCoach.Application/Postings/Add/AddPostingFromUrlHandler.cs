using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Postings.Fetching;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Add;

public sealed record AddPostingFromUrlCommand(string? Url);

public sealed class AddPostingFromUrlHandler(IPageFetcher fetcher, PostingImporter importer)
{
    public const int MinPageTextLength = 200;

    private const string PasteHint = "Paste the posting text instead.";

    public async Task<PostingOutcome> HandleAsync(AddPostingFromUrlCommand command, CancellationToken cancellationToken)
    {
        var (uri, error) = PostingUrlPolicy.Check(command.Url);
        if (uri is null)
        {
            return PostingOutcome.Invalid(error!);
        }

        string html;
        try
        {
            html = await fetcher.FetchHtmlAsync(uri, cancellationToken);
        }
        catch (PageFetchException ex)
        {
            return PostingOutcome.NeedsPaste($"{ex.Message} {PasteHint}");
        }

        var known = JobPostingJsonLd.TryRead(html);
        var text = known is null
            ? HtmlText.ToText(html)
            : string.Join("\n", new[] { known.Title, known.Company, known.City, "", known.Description }
                .Where(part => part is not null));

        if (text.Length < MinPageTextLength)
        {
            return PostingOutcome.NeedsPaste($"We could not read a posting from that page (it may need JavaScript or a sign-in). {PasteHint}");
        }

        if (text.Length > Posting.RawTextMaxLength)
        {
            text = text[..Posting.RawTextMaxLength];
        }

        return await importer.ImportAsync(text, uri.ToString(), known, cancellationToken);
    }
}
