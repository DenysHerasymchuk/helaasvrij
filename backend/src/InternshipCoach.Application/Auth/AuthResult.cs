using InternshipCoach.Domain.Users;

namespace InternshipCoach.Application.Auth;

public enum AuthStatus
{
    Ok,
    Invalid,
    EmailTaken,
    InvalidCredentials
}

public sealed record AuthResult(
    AuthStatus Status,
    User? User = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public bool Succeeded => Status == AuthStatus.Ok;

    public static AuthResult Ok(User user) => new(AuthStatus.Ok, user);

    public static AuthResult EmailTaken() => new(AuthStatus.EmailTaken);

    public static AuthResult InvalidCredentials() => new(AuthStatus.InvalidCredentials);

    public static AuthResult Invalid(IReadOnlyDictionary<string, string[]> errors) => new(AuthStatus.Invalid, Errors: errors);
}
