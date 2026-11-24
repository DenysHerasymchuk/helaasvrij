using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Documents;

public enum DocumentKind
{
    Cv,
    CoverLetter,
    RecruiterMessage
}

// A draft the student keeps, edits and copies. Content is markdown.
public sealed class Document : IUserOwned
{
    public const int TitleMaxLength = 200;
    public const int ContentMaxLength = 50_000;
    public const int LanguageMaxLength = 2;
    public static readonly IReadOnlyList<string> Languages = ["nl", "fr", "en"];

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public DocumentKind Kind { get; private set; }
    public string Language { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public CreatedAt CreatedAt { get; private set; } = null!;
    public DateTimeOffset UpdatedAt { get; private set; }

    private Document()
    {
    }

    public static bool IsLanguage(string? language) => language is not null && Languages.Contains(language);

    public static Document Create(Guid userId, DocumentKind kind, string? language, string? title, string? content, CreatedAt createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        if (!Enum.IsDefined(kind))
        {
            throw new DomainException("Unknown document kind.");
        }

        if (!IsLanguage(language))
        {
            throw new DomainException("Language must be nl, fr or en.");
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Kind = kind,
            Language = language!,
            CreatedAt = createdAt ?? throw new DomainException("Creation time is required.")
        };
        document.Edit(title, content, createdAt.Value);
        return document;
    }

    public void Edit(string? title, string? content, DateTimeOffset now)
    {
        var trimmedTitle = title?.Trim() ?? string.Empty;
        if (trimmedTitle.Length is 0 or > TitleMaxLength)
        {
            throw new DomainException($"Title must be 1 to {TitleMaxLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new DomainException("Content is required.");
        }

        if (content.Length > ContentMaxLength)
        {
            throw new DomainException($"Content can be at most {ContentMaxLength} characters.");
        }

        Title = trimmedTitle;
        Content = content;
        UpdatedAt = now.ToUniversalTime();
    }
}
