namespace InternshipCoach.Contracts.Interview;

public sealed record SubmitAnswerRequest(Guid PostingId, Guid QuestionId, string? Answer);

/// <param name="Score">1 (weak) to 5 (interview-ready); null when not rated.</param>
public sealed record AnswerResponse(Guid Id, string Answer, int? Score, string Good, string Add, string Example, DateTimeOffset CreatedAt);

/// <param name="Source">gap, repo or company.</param>
/// <param name="Project">The student's repo the question is about; null for company questions.</param>
/// <param name="Gap">The posting requirement a gap question comes from; null otherwise.</param>
/// <param name="Answers">The student's practice answers, newest first.</param>
public sealed record QuestionResponse(Guid Id, string Source, string Text, string? Project, string? Gap, IReadOnlyList<AnswerResponse> Answers);

/// <param name="Analysed">False until the posting has a gap analysis; then there are no own questions yet, only company ones.</param>
public sealed record InterviewResponse(Guid PostingId, string Company, string Title, string Language, bool Analysed, IReadOnlyList<QuestionResponse> Questions);
