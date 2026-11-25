using InternshipCoach.Domain.Postings;

namespace InternshipCoach.Application.Postings.Add;

public enum PostingStatus
{
    Created,

    /// <summary>The user saved this before; the posting is the existing one.</summary>
    Existing,

    /// <summary>The input is not acceptable (empty, too short, blocked link).</summary>
    Invalid,

    /// <summary>The page could not be read; the user should paste the text instead.</summary>
    NeedsPaste,

    /// <summary>An existing posting got new text and requirements.</summary>
    Updated,

    /// <summary>The thing to import does not exist (an Adzuna job that left the snapshot).</summary>
    NotFound
}

public sealed record PostingOutcome(PostingStatus Status, Posting? Posting, string? Message)
{
    public static PostingOutcome Created(Posting posting) => new(PostingStatus.Created, posting, null);

    public static PostingOutcome Existing(Posting posting) => new(PostingStatus.Existing, posting, null);

    public static PostingOutcome Updated(Posting posting) => new(PostingStatus.Updated, posting, null);

    public static PostingOutcome Invalid(string message) => new(PostingStatus.Invalid, null, message);

    public static PostingOutcome NeedsPaste(string message) => new(PostingStatus.NeedsPaste, null, message);

    public static PostingOutcome NotFound(string message) => new(PostingStatus.NotFound, null, message);
}
