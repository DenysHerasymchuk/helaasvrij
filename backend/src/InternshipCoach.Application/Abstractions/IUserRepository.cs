using InternshipCoach.Domain.Users;

namespace InternshipCoach.Application.Abstractions;

public interface IUserRepository
{
    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken);

    Task<User?> FindByGitHubIdAsync(long gitHubId, CancellationToken cancellationToken);

    // Persist immediately. Throws EmailAlreadyExistsException when the unique email index is hit.
    Task AddAsync(User user, CancellationToken cancellationToken);

    Task UpdateAsync(User user, CancellationToken cancellationToken);
}
