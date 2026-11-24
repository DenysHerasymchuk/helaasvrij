namespace InternshipCoach.Contracts.Interview;

public sealed record SubmitAnswerRequest(Guid PostingId, Guid QuestionId, string? Answer);

public sealed record AnswerResponse(Guid Id, string Answer, string Good, string Add, string Example, DateTimeOffset CreatedAt);

/// <param name="Source">gap, repo or company.</param>
/// <param name="Project">The student's repo the question is about; null for company questions.</param>
/// <param name="Answers">The student's practice answers, newest first.</param>
public sealed record QuestionResponse(Guid Id, string Source, string Text, string? Project, IReadOnlyList<AnswerResponse> Answers);

public sealed record InterviewResponse(Guid PostingId, string Company, string Title, string Language, IReadOnlyList<QuestionResponse> Questions);
