using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Postings.Fetching;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Add;

/// <summary>Shared tail of both add flows: extract with the cheap model, build the posting, save it.</summary>
public sealed class PostingImporter(PostingExtractor extractor, IPostingRepository postings, ICurrentUser currentUser, IClock clock)
{
    public async Task<PostingOutcome> ImportAsync(
        string text, string? url, JobPostingData? known, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var extracted = await extractor.ExtractAsync(userId, text, cancellationToken);

        if (!PostingLanguages.TryParse(extracted.Language, out var language))
        {
            language = PostingLanguage.En;
        }

        // Facts that the page states in structured data win over what the model read.
        var company = FirstText(known?.Company, extracted.Company);
        var title = FirstText(known?.Title, extracted.Title);
        var city = FirstText(known?.City, extracted.City);
        var deadline = known?.Deadline ?? PostingExtractor.ParseDate(extracted.Deadline);

        var requirements = extracted.Requirements
            .Select(r => (r.Text, PostingExtractor.ParseImportance(r.Importance)));

        Posting posting;
        try
        {
            posting = Posting.Create(userId, company, title, city, language, url, text, deadline, requirements,
                isDemo: false, new CreatedAt(clock.UtcNow));
            var stageStart = PostingExtractor.ParseDate(extracted.StageStart);
            var stageEnd = PostingExtractor.ParseDate(extracted.StageEnd);
            if (stageStart is null || stageEnd is null || stageEnd >= stageStart)
            {
                posting.SetStagePeriod(stageStart, stageEnd);
            }
        }
        catch (DomainException ex)
        {
            return PostingOutcome.NeedsPaste($"{ex.Message} Paste the posting text instead.");
        }

        await postings.AddAsync(posting, cancellationToken);
        return PostingOutcome.Created(posting);
    }

    private static string FirstText(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback ?? string.Empty;
}
