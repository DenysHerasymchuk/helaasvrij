namespace InternshipCoach.Contracts.Tokens;

public sealed record CreateApiTokenRequest(string? Name);

public sealed record ApiTokenResponse(Guid Id, string Name, string Hint, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, bool Revoked);

/// <summary>The secret is only in this response; it cannot be read again.</summary>
public sealed record CreatedApiTokenResponse(ApiTokenResponse Token, string Secret);
