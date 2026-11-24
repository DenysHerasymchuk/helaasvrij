using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Postings;

public sealed class Posting : IUserOwned
{
    public const int CompanyMaxLength = 200;
    public const int TitleMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int UrlMaxLength = 2000;
    public const int RawTextMaxLength = 20000;
    public const int MaxRequirements = 30;

    private readonly List<Requirement> _requirements = [];

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Company { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public PostingLanguage Language { get; private set; }
    public string? Url { get; private set; }
    public string RawText { get; private set; } = null!;
    public DateOnly? Deadline { get; private set; }
    public DateTimeOffset? InterviewAt { get; private set; }
    public bool IsDemo { get; private set; }
    public CreatedAt CreatedAt { get; private set; } = null!;

    public IReadOnlyList<Requirement> Requirements => _requirements;

    private Posting()
    {
    }

    public static Posting Create(
        Guid userId, string company, string title, string? city, PostingLanguage language, string? url, string rawText,
        DateOnly? deadline, IEnumerable<(string Text, Importance Importance)> requirements, bool isDemo, CreatedAt createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        if (string.IsNullOrWhiteSpace(company))
        {
            throw new DomainException("Company is required.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(rawText))
        {
            throw new DomainException("Posting text is required.");
        }

        var posting = new Posting
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Company = Truncate(company.Trim(), CompanyMaxLength),
            Title = Truncate(title.Trim(), TitleMaxLength),
            City = Truncate((city ?? string.Empty).Trim(), CityMaxLength),
            Language = language,
            Url = string.IsNullOrWhiteSpace(url) ? null : Truncate(url.Trim(), UrlMaxLength),
            RawText = Truncate(rawText.Trim(), RawTextMaxLength),
            Deadline = deadline,
            IsDemo = isDemo,
            CreatedAt = createdAt ?? throw new DomainException("Creation time is required.")
        };

        var position = 0;
        foreach (var (text, importance) in requirements.Where(r => !string.IsNullOrWhiteSpace(r.Text)).Take(MaxRequirements))
        {
            posting._requirements.Add(Requirement.Create(userId, posting.Id, position++, text, importance));
        }

        return posting;
    }

    public void SetDates(DateOnly? deadline, DateTimeOffset? interviewAt)
    {
        Deadline = deadline;
        InterviewAt = interviewAt?.ToUniversalTime();
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}
