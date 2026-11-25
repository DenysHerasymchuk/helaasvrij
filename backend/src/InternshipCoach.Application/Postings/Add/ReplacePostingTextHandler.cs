using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Add;

public sealed record ReplacePostingTextCommand(Guid Id, string? Text);

/// <summary>
/// Swaps a short posting text (an Adzuna snippet) for the full ad the student pasted and re-reads the requirements.
/// Company, title and dates stay as they are: the student may have edited the dates already.
/// </summary>
public sealed class ReplacePostingTextHandler(PostingExtractor extractor, IPostingRepository postings, ICurrentUser currentUser)
{
    public async Task<PostingOutcome> HandleAsync(ReplacePostingTextCommand command, CancellationToken cancellationToken)
    {
        var text = command.Text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length < AddPostingFromPasteHandler.MinTextLength)
        {
            return PostingOutcome.Invalid($"Paste the full posting text (at least {AddPostingFromPasteHandler.MinTextLength} characters).");
        }

        if (text.Length > Posting.RawTextMaxLength)
        {
            return PostingOutcome.Invalid($"The posting is too long (max {Posting.RawTextMaxLength} characters).");
        }

        var posting = await postings.FindAsync(command.Id, cancellationToken);
        if (posting is null)
        {
            return PostingOutcome.NotFound("This posting does not exist.");
        }

        var extracted = await extractor.ExtractAsync(currentUser.UserId, text, cancellationToken);
        var language = PostingLanguages.TryParse(extracted.Language, out var parsed) ? parsed : posting.Language;
        posting.ReplaceText(text, language, extracted.Requirements.Select(r => (r.Text, PostingExtractor.ParseImportance(r.Importance))));
        await postings.SaveAsync(cancellationToken);
        return PostingOutcome.Updated(posting);
    }
}
