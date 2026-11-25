using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Users;

namespace InternshipCoach.Application.Auth.Login;

public sealed class LoginHandler(IUserRepository users, IPasswordHasher hasher)
{
    public async Task<AuthResult> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var password = command.Password ?? string.Empty;
        var user = Email.TryCreate(command.Email, out var email)
            ? await users.FindByEmailAsync(email, cancellationToken)
            : null;

        if (user?.PasswordHash is null)
        {
            // Unknown email or a GitHub-only account: spend the same hashing time, then fail generically.
            hasher.Hash(password);
            return AuthResult.InvalidCredentials();
        }

        return hasher.Verify(user.PasswordHash, password)
            ? AuthResult.Ok(user)
            : AuthResult.InvalidCredentials();
    }
}
