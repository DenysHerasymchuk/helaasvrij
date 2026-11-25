using System.Security.Cryptography;
using System.Text;
using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Tokens;

// A personal access token for tools such as the MCP server. Only the SHA-256 hash is stored; the token itself is shown once.
public sealed class ApiToken : IUserOwned
{
    public const int NameMaxLength = 60;
    public const int MaxActivePerUser = 10;
    public const string Prefix = "hv_";

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = null!;
    public string Hash { get; private set; } = null!;
    public string Hint { get; private set; } = null!;
    public CreatedAt CreatedAt { get; private set; } = null!;
    public DateTimeOffset? LastUsedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }

    private ApiToken()
    {
    }

    /// <summary>Makes a new token. <paramref name="secret" /> is the full text the user copies; it is not kept.</summary>
    public static ApiToken Create(Guid userId, string? name, CreatedAt createdAt, out string secret)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id is required.");
        }

        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new DomainException("Give the token a name, for example the tool that uses it.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException($"The name can be at most {NameMaxLength} characters.");
        }

        secret = Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return new ApiToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = trimmed,
            Hash = HashOf(secret),
            Hint = secret[^4..],
            CreatedAt = createdAt
        };
    }

    public static string HashOf(string secret) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

    public bool IsActive => RevokedAt is null;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now.ToUniversalTime();
}
