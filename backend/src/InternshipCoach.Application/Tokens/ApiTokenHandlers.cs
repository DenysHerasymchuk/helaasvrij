using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Tokens;

namespace InternshipCoach.Application.Tokens;

public sealed record ApiTokenView(Guid Id, string Name, string Hint, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt, bool Revoked);

public sealed record CreatedApiToken(ApiTokenView Token, string Secret);

public sealed class CreateApiTokenHandler(IApiTokenRepository tokens, ICurrentUser currentUser, IClock clock)
{
    /// <summary>Returns the secret once; it cannot be read again.</summary>
    public async Task<CreatedApiToken> HandleAsync(string? name, CancellationToken cancellationToken)
    {
        var active = (await tokens.ListAsync(cancellationToken)).Count(t => t.IsActive);
        if (active >= ApiToken.MaxActivePerUser)
        {
            throw new DomainException($"You can have {ApiToken.MaxActivePerUser} active tokens. Revoke one first.");
        }

        var token = ApiToken.Create(currentUser.UserId, name, new CreatedAt(clock.UtcNow), out var secret);
        await tokens.AddAsync(token, cancellationToken);
        return new CreatedApiToken(ListApiTokensHandler.ToView(token), secret);
    }
}

public sealed class ListApiTokensHandler(IApiTokenRepository tokens)
{
    public async Task<IReadOnlyList<ApiTokenView>> HandleAsync(CancellationToken cancellationToken) =>
        (await tokens.ListAsync(cancellationToken)).Select(ToView).ToList();

    public static ApiTokenView ToView(ApiToken t) => new(t.Id, t.Name, t.Hint, t.CreatedAt.Value, t.LastUsedAt, !t.IsActive);
}

public sealed class RevokeApiTokenHandler(IApiTokenRepository tokens, IClock clock)
{
    /// <summary>False when the token does not exist or belongs to someone else.</summary>
    public async Task<bool> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var token = await tokens.FindAsync(id, cancellationToken);
        if (token is null)
        {
            return false;
        }

        token.Revoke(clock.UtcNow);
        await tokens.SaveAsync(cancellationToken);
        return true;
    }
}
