using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Tokens;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

public sealed class ApiTokenRepository(InternshipCoachDbContext db) : IApiTokenRepository
{
    public async Task<IReadOnlyList<ApiToken>> ListAsync(CancellationToken cancellationToken) =>
        await db.ApiTokens.OrderByDescending(t => t.CreatedAt).ToListAsync(cancellationToken);

    public Task<ApiToken?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.ApiTokens.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task AddAsync(ApiToken token, CancellationToken cancellationToken)
    {
        db.ApiTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    // shortcut: writes last-used on every request through a set-based update (no SaveChanges, so no current user is needed);
    // throttle it when a token is used at high volume.
    public async Task<Guid?> ResolveAsync(string hash, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var token = await db.ApiTokens.IgnoreQueryFilters()
            .Where(t => t.Hash == hash && t.RevokedAt == null)
            .Select(t => new { t.Id, t.UserId })
            .FirstOrDefaultAsync(cancellationToken);
        if (token is null)
        {
            return null;
        }

        await db.ApiTokens.IgnoreQueryFilters().Where(t => t.Id == token.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(t => t.LastUsedAt, now.ToUniversalTime()), cancellationToken);
        return token.UserId;
    }
}
