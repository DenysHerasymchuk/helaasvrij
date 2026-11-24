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

    /// <summary>Below this percentage, or with too few required items covered, the posting is "no clear fit".</summary>
    public const int NoClearFitBelowScore = 40;

    private readonly List<Requirement> _requirements = [];
    private readonly List<ScoreSnapshot> _snapshots = [];

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

    /// <summary>When the internship itself runs, if the posting says so.</summary>
    public DateOnly? StageStart { get; private set; }
    public DateOnly? StageEnd { get; private set; }
    public bool IsDemo { get; private set; }
    public CreatedAt CreatedAt { get; private set; } = null!;

    public IReadOnlyList<Requirement> Requirements => _requirements;

    /// <summary>Only loaded when the repository includes them.</summary>
    public IReadOnlyCollection<ScoreSnapshot> Snapshots => _snapshots;

    public bool IsAnalysed => _requirements.Any(r => r.AnalysedAt is not null);

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

    /// <summary>Stores the internship period. An end before the start is a <see cref="DomainException"/>.</summary>
    public void SetStagePeriod(DateOnly? start, DateOnly? end)
    {
        if (start is not null && end is not null && end < start)
        {
            throw new DomainException("The stage end date must be on or after the start date.");
        }

        StageStart = start;
        StageEnd = end;
    }

    /// <summary>
    /// Compares the posting's internship dates with the student's school stage period. Every known posting date
    /// must fall inside the student's period (an open side accepts anything). Unknown when either side has no dates.
    /// </summary>
    public StageFit CheckStage(DateOnly? studentStart, DateOnly? studentEnd)
    {
        DateOnly?[] dates = [StageStart, StageEnd];
        if ((studentStart is null && studentEnd is null) || dates.All(d => d is null))
        {
            return StageFit.Unknown;
        }

        var outside = dates.Any(d => d is { } date && (date < studentStart || date > studentEnd));
        return outside ? StageFit.Conflict : StageFit.Fits;
    }

    /// <summary>
    /// sum(weight x coverage) / sum(weight) as a whole percentage (half rounds up). Required 2, nice 1;
    /// strong 1, partial 0.5, none 0. No requirements gives 0. Same input, same score.
    /// </summary>
    public int Score()
    {
        var total = _requirements.Sum(r => r.Importance.Weight());
        if (total == 0)
        {
            return 0;
        }

        var earnedHalfUnits = _requirements.Sum(r => r.Importance.Weight() * r.Coverage.HalfUnits());
        return (int)Math.Round(100m * earnedHalfUnits / (2m * total), MidpointRounding.AwayFromZero);
    }

    /// <summary>True when the score is below 40% or fewer than half of the required items are at least Partial.</summary>
    public bool NoClearFit()
    {
        if (Score() < NoClearFitBelowScore)
        {
            return true;
        }

        var required = _requirements.Where(r => r.Importance == Importance.Required).ToList();
        var covered = required.Count(r => r.Coverage != Coverage.None);
        return covered * 2 < required.Count;
    }

    /// <summary>Stores the current score. Call after the requirements were (re)analysed.</summary>
    public ScoreSnapshot RecordSnapshot(string reason, DateTimeOffset takenAt)
    {
        var snapshot = ScoreSnapshot.Create(UserId, Id, Score(), reason, takenAt);
        _snapshots.Add(snapshot);
        return snapshot;
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}
