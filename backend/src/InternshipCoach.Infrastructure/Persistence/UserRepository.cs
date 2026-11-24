using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InternshipCoach.Infrastructure.Persistence;

public sealed class UserRepository(InternshipCoachDbContext db) : IUserRepository
{
    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<User?> FindByGitHubIdAsync(long gitHubId, CancellationToken cancellationToken) =>
        db.Users.FirstOrDefaultAsync(user => user.GitHubId == gitHubId, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.Entry(user).State = EntityState.Detached;
            throw new EmailAlreadyExistsException();
        }
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
