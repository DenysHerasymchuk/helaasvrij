using System.Reflection;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

// Generic on purpose: it walks every IUserOwned entity type in the EF model, so tables added by later
// features are deleted without touching this class.
public sealed class AccountDeleter(InternshipCoachDbContext db) : IAccountDeleter
{
    private static readonly MethodInfo RemoveOwnedMethod = typeof(AccountDeleter)
        .GetMethod(nameof(RemoveOwnedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var ownedTypes = db.Model.GetEntityTypes()
            .Where(type => typeof(IUserOwned).IsAssignableFrom(type.ClrType) && type.BaseType is null)
            .Select(type => type.ClrType)
            .ToList();

        // Everything is tracked first and removed by a single SaveChanges, which is one database transaction.
        foreach (var clrType in ownedTypes)
        {
            await (Task)RemoveOwnedMethod.MakeGenericMethod(clrType).Invoke(this, [userId, cancellationToken])!;
        }

        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is not null)
        {
            db.Users.Remove(user);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveOwnedAsync<T>(Guid userId, CancellationToken cancellationToken) where T : class, IUserOwned
    {
        var rows = await db.Set<T>().IgnoreQueryFilters().Where(row => row.UserId == userId).ToListAsync(cancellationToken);
        db.Set<T>().RemoveRange(rows);
    }
}
