using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Users;

namespace InternshipCoach.Application.Auth.Me;

public sealed class MeHandler(IUserRepository users)
{
    public Task<User?> HandleAsync(Guid userId, CancellationToken cancellationToken) =>
        users.FindByIdAsync(userId, cancellationToken);
}
