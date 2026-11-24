using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Common;
using InternshipCoach.Domain.Users;

namespace InternshipCoach.Application.Auth.Register;

public sealed class RegisterHandler(IUserRepository users, IPasswordHasher hasher, IClock clock)
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 128;

    public async Task<AuthResult> HandleAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            errors["name"] = ["Name is required."];
        }

        if (!Email.TryCreate(command.Email, out var email))
        {
            errors["email"] = ["Enter a valid email address."];
        }

        var password = command.Password ?? string.Empty;
        if (password.Length < MinPasswordLength || password.Length > MaxPasswordLength)
        {
            errors["password"] = [$"Password must be {MinPasswordLength} to {MaxPasswordLength} characters."];
        }

        if (errors.Count > 0)
        {
            return AuthResult.Invalid(errors);
        }

        if (await users.FindByEmailAsync(email, cancellationToken) is not null)
        {
            return AuthResult.EmailTaken();
        }

        var user = User.CreateWithPassword(email, command.Name!, hasher.Hash(password), new CreatedAt(clock.UtcNow));

        try
        {
            await users.AddAsync(user, cancellationToken);
        }
        catch (EmailAlreadyExistsException)
        {
            return AuthResult.EmailTaken();
        }

        return AuthResult.Ok(user);
    }
}
