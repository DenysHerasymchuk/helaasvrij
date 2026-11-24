using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Users;

namespace InternshipCoach.Application.Tests.TestDoubles;

public sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(user => user.Id == id));

    public Task<User?> FindByEmailAsync(Email email, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(user => user.Email.Equals(email)));

    public Task<User?> FindByGitHubIdAsync(long gitHubId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.FirstOrDefault(user => user.GitHubId == gitHubId));

    public Task AddAsync(User user, CancellationToken cancellationToken)
    {
        if (Users.Any(existing => existing.Email.Equals(user.Email)))
        {
            throw new EmailAlreadyExistsException();
        }

        Users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken) => Task.CompletedTask;
}
