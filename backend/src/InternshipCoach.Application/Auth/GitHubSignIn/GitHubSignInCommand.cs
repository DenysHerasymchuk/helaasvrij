namespace InternshipCoach.Application.Auth.GitHubSignIn;

// VerifiedEmail is only set when GitHub reports the address as verified.
public sealed record GitHubSignInCommand(
    long GitHubId,
    string Login,
    string? Name,
    string? VerifiedEmail,
    string? AvatarUrl);
