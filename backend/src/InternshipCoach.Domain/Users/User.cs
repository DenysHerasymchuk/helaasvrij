using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Users;

public sealed class User
{
    public const int DisplayNameMaxLength = 100;

    public Guid Id { get; private set; }
    public Email Email { get; private set; } = null!;
    public string DisplayName { get; private set; } = null!;
    public string? PasswordHash { get; private set; }
    public long? GitHubId { get; private set; }
    public string? GitHubLogin { get; private set; }
    public string? AvatarUrl { get; private set; }
    public CreatedAt CreatedAt { get; private set; } = null!;

    private User()
    {
    }

    public static User CreateWithPassword(Email email, string displayName, string passwordHash, CreatedAt createdAt)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.");
        }

        return new User
        {
            Id = Guid.NewGuid(),
            Email = email ?? throw new DomainException("Email is required."),
            DisplayName = NormaliseDisplayName(displayName),
            PasswordHash = passwordHash,
            CreatedAt = createdAt ?? throw new DomainException("Creation time is required.")
        };
    }

    public static User CreateFromGitHub(
        Email email, string displayName, long gitHubId, string gitHubLogin, string? avatarUrl, CreatedAt createdAt)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email ?? throw new DomainException("Email is required."),
            DisplayName = NormaliseDisplayName(displayName),
            CreatedAt = createdAt ?? throw new DomainException("Creation time is required.")
        };

        user.LinkGitHub(gitHubId, gitHubLogin, avatarUrl);
        return user;
    }

    public void LinkGitHub(long gitHubId, string gitHubLogin, string? avatarUrl)
    {
        if (gitHubId <= 0)
        {
            throw new DomainException("GitHub id is not valid.");
        }

        if (string.IsNullOrWhiteSpace(gitHubLogin))
        {
            throw new DomainException("GitHub login is required.");
        }

        GitHubId = gitHubId;
        GitHubLogin = gitHubLogin.Trim();
        AvatarUrl = string.IsNullOrWhiteSpace(avatarUrl) ? null : avatarUrl.Trim();
    }

    private static string NormaliseDisplayName(string? displayName)
    {
        var name = displayName?.Trim();

        if (string.IsNullOrEmpty(name))
        {
            throw new DomainException("Display name is required.");
        }

        return name.Length > DisplayNameMaxLength ? name[..DisplayNameMaxLength] : name;
    }
}
