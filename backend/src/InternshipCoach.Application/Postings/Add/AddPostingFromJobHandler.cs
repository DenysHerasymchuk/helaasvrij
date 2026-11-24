using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Postings.Fetching;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Add;

public sealed record AddPostingFromJobCommand(string? JobId);

/// <summary>
/// Saves an Adzuna ad as the user's own posting. The ad is looked up in the shared snapshot by id, so title, company
/// and text never come from the client. Adzuna's facts win over what the model reads; the model adds language and requirements.
/// </summary>
public sealed class AddPostingFromJobHandler(IJobSnapshot snapshot, PostingImporter importer)
{
    public async Task<PostingOutcome> HandleAsync(AddPostingFromJobCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.JobId))
        {
            return PostingOutcome.Invalid("Choose a job to save.");
        }

        var jobs = await snapshot.LoadAsync(cancellationToken);
        var job = jobs?.FirstOrDefault(j => j.Id == command.JobId);
        if (job is null)
        {
            return PostingOutcome.NotFound("This job is no longer in the list. Search again.");
        }

        var company = string.IsNullOrWhiteSpace(job.Company) ? "Unknown company" : job.Company;
        var description = job.Description ?? string.Empty;
        var text = string.Join("\n", new[]
        {
            job.Title,
            string.Join(", ", new[] { company, job.Location }.Where(s => !string.IsNullOrWhiteSpace(s))),
            job.Created is { Length: >= 10 } created ? $"Posted {created[..10]}" : null,
            "",
            description
        }.Where(line => line is not null));
        if (text.Length > Posting.RawTextMaxLength)
        {
            text = text[..Posting.RawTextMaxLength];
        }

        var url = PostingUrlPolicy.Check(job.Url).Uri?.ToString();
        return await importer.ImportAsync(text, url, new JobPostingData(job.Title, company, job.Location, null, description), cancellationToken);
    }
}
