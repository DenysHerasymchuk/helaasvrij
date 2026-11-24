using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Interview;

// Shared and anonymous: an interview question per company. Deliberately NOT IUserOwned: no UserId, no user data.
public sealed class CompanyQuestion
{
    public const int CompanyMaxLength = 200;
    public const int TextMaxLength = 600;

    public Guid Id { get; private set; }

    /// <summary>Lower-case, trimmed company name used for matching (see <see cref="KeyFor" />).</summary>
    public string CompanyKey { get; private set; } = null!;

    public string Company { get; private set; } = null!;
    public string Text { get; private set; } = null!;

    private CompanyQuestion()
    {
    }

    public static string KeyFor(string? company) =>
        string.Join(' ', (company ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    public static CompanyQuestion Create(string? company, string? text)
    {
        var key = KeyFor(company);
        var trimmedText = text?.Trim() ?? string.Empty;
        if (key.Length is 0 or > CompanyMaxLength)
        {
            throw new DomainException("Company is required.");
        }

        if (trimmedText.Length is 0 or > TextMaxLength)
        {
            throw new DomainException($"Question must be 1 to {TextMaxLength} characters.");
        }

        return new CompanyQuestion { Id = Guid.NewGuid(), CompanyKey = key, Company = company!.Trim(), Text = trimmedText };
    }
}
