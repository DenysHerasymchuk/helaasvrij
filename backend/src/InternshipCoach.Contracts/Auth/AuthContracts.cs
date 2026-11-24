namespace InternshipCoach.Contracts.Auth;

public sealed record RegisterRequest(string? Name, string? Email, string? Password);

public sealed record LoginRequest(string? Email, string? Password, bool RememberMe);

public sealed record MeResponse(Guid Id, string Email, string DisplayName, string? AvatarUrl, string? GitHubLogin);
