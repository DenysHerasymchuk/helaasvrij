using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Add;

public enum PostingStatus
{
    Created,

    /// <summary>The input is not acceptable (empty, too short, blocked link).</summary>
    Invalid,

    /// <summary>The page could not be read; the user should paste the text instead.</summary>
    NeedsPaste
}

public sealed record PostingOutcome(PostingStatus Status, Posting? Posting, string? Message)
{
    public static PostingOutcome Created(Posting posting) => new(PostingStatus.Created, posting, null);

    public static PostingOutcome Invalid(string message) => new(PostingStatus.Invalid, null, message);

    public static PostingOutcome NeedsPaste(string message) => new(PostingStatus.NeedsPaste, null, message);
}
