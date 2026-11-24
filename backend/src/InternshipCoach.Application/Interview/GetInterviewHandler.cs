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
/// A posting that is not the user's is "not found" (the repository filters by user).
/// </summary>
public sealed class GetInterviewHandler(IPostingRepository postings, IInterviewRepository interview, InterviewQuestionGenerator generator)
{
    public async Task<InterviewView?> HandleAsync(Guid postingId, CancellationToken ct)
    {
        var posting = await postings.FindAsync(postingId, ct);
        if (posting is null)
        {
            return null;
        }

        var questions = await interview.ListQuestionsAsync(postingId, ct);
        if (questions.Count == 0)
        {
            questions = await interview.AddQuestionsAsync(await generator.GenerateAsync(posting, ct), ct);
        }

        var company = await interview.ListCompanyQuestionsAsync(CompanyQuestion.KeyFor(posting.Company), ct);
        var answers = await interview.ListAnswersAsync(postingId, ct);
        return new InterviewView(posting, questions, company, answers);
    }
}
