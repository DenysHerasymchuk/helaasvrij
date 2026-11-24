using InternshipCoach.Application.Interview;
using InternshipCoach.Domain.Interview;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeInterviewRepository(Guid userId) : IInterviewRepository
{
    public List<InterviewQuestion> Questions { get; } = [];
    public List<CompanyQuestion> Company { get; } = [];
    public List<InterviewAnswer> Answers { get; } = [];

    // Mimics the user query filter.
    public Task<IReadOnlyList<InterviewQuestion>> ListQuestionsAsync(Guid postingId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InterviewQuestion>>(Questions.Where(q => q.PostingId == postingId && q.UserId == userId).OrderBy(q => q.Position).ToList());

    public Task<IReadOnlyList<InterviewQuestion>> AddQuestionsAsync(IReadOnlyList<InterviewQuestion> questions, CancellationToken ct)
    {
        Questions.AddRange(questions);
        return Task.FromResult(questions);
    }

    public Task<IReadOnlyList<CompanyQuestion>> ListCompanyQuestionsAsync(string companyKey, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CompanyQuestion>>(Company.Where(c => c.CompanyKey == companyKey).ToList());

    public Task<CompanyQuestion?> FindCompanyQuestionAsync(Guid id, CancellationToken ct) => Task.FromResult(Company.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<InterviewAnswer>> ListAnswersAsync(Guid postingId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InterviewAnswer>>(Answers.Where(a => a.PostingId == postingId && a.UserId == userId).ToList());

    public Task<int> CountAnswersAsync(Guid questionId, CancellationToken ct) => Task.FromResult(Answers.Count(a => a.QuestionId == questionId && a.UserId == userId));

    public Task AddAnswerAsync(InterviewAnswer answer, CancellationToken ct)
    {
        Answers.Add(answer);
        return Task.CompletedTask;
    }
}
