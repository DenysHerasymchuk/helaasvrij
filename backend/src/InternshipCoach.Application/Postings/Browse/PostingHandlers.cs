using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Browse;

public sealed record ListPostingsQuery(string? Language, string? Text);

public sealed class ListPostingsHandler(IPostingRepository postings)
{
    public Task<IReadOnlyList<Posting>> HandleAsync(ListPostingsQuery query, CancellationToken cancellationToken)
    {
        PostingLanguage? language = PostingLanguages.TryParse(query.Language, out var parsed) ? parsed : null;
        var text = string.IsNullOrWhiteSpace(query.Text) ? null : query.Text.Trim();
        return postings.ListAsync(language, text, cancellationToken);
    }
}

public sealed class GetPostingHandler(IPostingRepository postings)
{
    public Task<Posting?> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        postings.FindAsync(id, cancellationToken);
}

public sealed record UpdatePostingDatesCommand(Guid Id, DateOnly? Deadline, DateTimeOffset? InterviewAt);

public sealed class UpdatePostingDatesHandler(IPostingRepository postings)
{
    public async Task<Posting?> HandleAsync(UpdatePostingDatesCommand command, CancellationToken cancellationToken)
    {
        var posting = await postings.FindAsync(command.Id, cancellationToken);
        if (posting is null)
        {
            return null;
        }

        posting.SetDates(command.Deadline, command.InterviewAt);
        await postings.SaveAsync(cancellationToken);
        return posting;
    }
}

public sealed class DeletePostingHandler(IPostingRepository postings)
{
    public async Task<bool> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var posting = await postings.FindAsync(id, cancellationToken);
        if (posting is null)
        {
            return false;
        }

        await postings.DeleteAsync(posting, cancellationToken);
        return true;
    }
}
