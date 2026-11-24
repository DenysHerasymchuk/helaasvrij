using InternshipCoach.Domain.Interview;

namespace InternshipCoach.Application.Interview;

// Questions and answers see only the signed-in user's rows (global query filter). Company questions are shared.
public interface IInterviewRepository
{
    /// <summary>The user's questions for a posting, in the order they were generated.</summary>
    Task<IReadOnlyList<InterviewQuestion>> ListQuestionsAsync(Guid postingId, CancellationToken ct);

    Task<IReadOnlyList<InterviewQuestion>> AddQuestionsAsync(IReadOnlyList<InterviewQuestion> questions, CancellationToken ct);

    /// <summary>Deletes the user's questions for the posting and the answers to them, then stores the new ones. Company-question answers stay.</summary>
    Task<IReadOnlyList<InterviewQuestion>> ReplaceQuestionsAsync(Guid postingId, IReadOnlyList<InterviewQuestion> questions, CancellationToken ct);

    Task<IReadOnlyList<CompanyQuestion>> ListCompanyQuestionsAsync(string companyKey, CancellationToken ct);

    Task<CompanyQuestion?> FindCompanyQuestionAsync(Guid id, CancellationToken ct);

    /// <summary>The user's answers for a posting, newest first.</summary>
    Task<IReadOnlyList<InterviewAnswer>> ListAnswersAsync(Guid postingId, CancellationToken ct);

    Task<int> CountAnswersAsync(Guid questionId, CancellationToken ct);

    Task AddAnswerAsync(InterviewAnswer answer, CancellationToken ct);
}
