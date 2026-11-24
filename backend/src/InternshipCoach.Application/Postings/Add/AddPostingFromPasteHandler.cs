using InternshipCoach.Application.Postings.Fetching;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Add;

public sealed record AddPostingFromPasteCommand(string? Text, string? Url);

public sealed class AddPostingFromPasteHandler(PostingImporter importer)
{
    public const int MinTextLength = 50;

    public async Task<PostingOutcome> HandleAsync(AddPostingFromPasteCommand command, CancellationToken cancellationToken)
    {
        var text = command.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length < MinTextLength)
        {
            return PostingOutcome.Invalid($"Paste the full posting text (at least {MinTextLength} characters).");
        }

        if (text.Length > Posting.RawTextMaxLength)
        {
            return PostingOutcome.Invalid($"The posting is too long (max {Posting.RawTextMaxLength} characters).");
        }

        string? url = null;
        if (!string.IsNullOrWhiteSpace(command.Url))
        {
            var (uri, error) = PostingUrlPolicy.Check(command.Url);
            if (uri is null)
            {
                return PostingOutcome.Invalid(error!);
            }

            url = uri.ToString();
        }

        return await importer.ImportAsync(text, url, known: null, cancellationToken);
    }
}
