using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Interview;
using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Interview;

public sealed record InterviewView(
    Posting Posting,
    IReadOnlyList<InterviewQuestion> Questions,
    IReadOnlyList<CompanyQuestion> CompanyQuestions,
    IReadOnlyList<InterviewAnswer> Answers);

/// <summary>
/// The practice questions of one of the user's postings. Generated on the first call and stored; later calls only read.
/// A posting that is not analysed yet gets no model call: the view has no own questions and the page offers "Analyse this posting first".
/// <c>regenerate</c> writes a new set first and only then replaces the old one (and its answers), so a failed call keeps the old set.
/// A posting that is not the user's is "not found" (the repository filters by user).
/// </summary>
public sealed class GetInterviewHandler(IPostingRepository postings, IInterviewRepository interview, InterviewQuestionGenerator generator)
{
    public async Task<InterviewView?> HandleAsync(Guid postingId, CancellationToken ct, bool regenerate = false)
    {
        var posting = await postings.FindAsync(postingId, ct);
        if (posting is null)
        {
            return null;
        }

        var questions = await interview.ListQuestionsAsync(postingId, ct);
        if (regenerate)
        {
            questions = await interview.ReplaceQuestionsAsync(postingId, await generator.GenerateAsync(posting, ct), ct);
        }
        else if (questions.Count == 0 && posting.IsAnalysed)
        {
            var generated = await generator.GenerateAsync(posting, ct);
            // A second request for the same posting may have stored a set while this one waited on the model: keep that one.
            questions = await interview.ListQuestionsAsync(postingId, ct);
            if (questions.Count == 0)
            {
                questions = await interview.AddQuestionsAsync(generated, ct);
            }
        }

        var company = await interview.ListCompanyQuestionsAsync(CompanyQuestion.KeyFor(posting.Company), ct);
        var answers = await interview.ListAnswersAsync(postingId, ct);
        return new InterviewView(posting, questions, company, answers);
    }
}
