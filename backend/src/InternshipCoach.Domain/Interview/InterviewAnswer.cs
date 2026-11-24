using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Interview;

// One practice answer and the short feedback on it. QuestionId points to an InterviewQuestion or a CompanyQuestion.
public sealed class InterviewAnswer : IUserOwned
{
    public const int AnswerMaxLength = 3000;
    public const int FeedbackMaxLength = 600;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid PostingId { get; private set; }
    public Guid QuestionId { get; private set; }
    public string Answer { get; private set; } = null!;
    public string WhatWasGood { get; private set; } = null!;
    public string WhatToAdd { get; private set; } = null!;
    public string ExampleSentence { get; private set; } = null!;
    public CreatedAt CreatedAt { get; private set; } = null!;

    private InterviewAnswer()
    {
    }

    public static InterviewAnswer Create(
        Guid userId, Guid postingId, Guid questionId, string? answer, string? good, string? add, string? example, CreatedAt createdAt)
    {
        if (userId == Guid.Empty || postingId == Guid.Empty || questionId == Guid.Empty)
        {
            throw new DomainException("User, posting and question are required.");
        }

        var trimmed = answer?.Trim() ?? string.Empty;
        if (trimmed.Length is 0 or > AnswerMaxLength)
        {
            throw new DomainException($"Answer must be 1 to {AnswerMaxLength} characters.");
        }

        return new InterviewAnswer
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PostingId = postingId,
            QuestionId = questionId,
            Answer = trimmed,
            WhatWasGood = Cap(good),
            WhatToAdd = Cap(add),
            ExampleSentence = Cap(example),
            CreatedAt = createdAt ?? throw new DomainException("Creation time is required.")
        };
    }

    private static string Cap(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        return trimmed.Length > FeedbackMaxLength ? trimmed[..FeedbackMaxLength] : trimmed;
    }
}
