using InternshipCoach.Application.Interview;
using InternshipCoach.Domain.Interview;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

public sealed class InterviewRepository(InternshipCoachDbContext db) : IInterviewRepository
{
    public async Task<IReadOnlyList<InterviewQuestion>> ListQuestionsAsync(Guid postingId, CancellationToken ct) =>
        await db.InterviewQuestions.Where(q => q.PostingId == postingId).OrderBy(q => q.Position).ToListAsync(ct);

    public async Task<IReadOnlyList<InterviewQuestion>> AddQuestionsAsync(IReadOnlyList<InterviewQuestion> questions, CancellationToken ct)
    {
        db.InterviewQuestions.AddRange(questions);
        await db.SaveChangesAsync(ct);
        return questions;
    }

    public async Task<IReadOnlyList<InterviewQuestion>> ReplaceQuestionsAsync(Guid postingId, IReadOnlyList<InterviewQuestion> questions, CancellationToken ct)
    {
        var old = await db.InterviewQuestions.Where(q => q.PostingId == postingId).ToListAsync(ct);
        var oldIds = old.Select(q => q.Id).ToList();
        db.InterviewAnswers.RemoveRange(await db.InterviewAnswers.Where(a => oldIds.Contains(a.QuestionId)).ToListAsync(ct));
        db.InterviewQuestions.RemoveRange(old);
        db.InterviewQuestions.AddRange(questions);
        await db.SaveChangesAsync(ct); // one SaveChanges = one transaction, so a crash never leaves the posting without a set
        return questions;
    }

    public async Task<IReadOnlyList<CompanyQuestion>> ListCompanyQuestionsAsync(string companyKey, CancellationToken ct) =>
        await db.CompanyQuestions.Where(q => q.CompanyKey == companyKey).OrderBy(q => q.Id).ToListAsync(ct);

    public Task<CompanyQuestion?> FindCompanyQuestionAsync(Guid id, CancellationToken ct) =>
        db.CompanyQuestions.FirstOrDefaultAsync(q => q.Id == id, ct);

    // shortcut: sorts in memory (CreatedAt is a converted value object); a posting has at most 5 answers per question.
    public async Task<IReadOnlyList<InterviewAnswer>> ListAnswersAsync(Guid postingId, CancellationToken ct) =>
        (await db.InterviewAnswers.Where(a => a.PostingId == postingId).ToListAsync(ct)).OrderByDescending(a => a.CreatedAt.Value).ToList();

    public Task<int> CountAnswersAsync(Guid questionId, CancellationToken ct) =>
        db.InterviewAnswers.CountAsync(a => a.QuestionId == questionId, ct);

    public async Task AddAnswerAsync(InterviewAnswer answer, CancellationToken ct)
    {
        db.InterviewAnswers.Add(answer);
        await db.SaveChangesAsync(ct);
    }
}
