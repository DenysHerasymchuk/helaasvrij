namespace InternshipCoach.Api.Auth;

public static class AuthSchemes
{
    public const string Cookie = "Cookies";
    public const string External = "External";
    public const string GitHub = "GitHub";
    public const string RateLimitPolicy = "auth";
    public const string VerifiedEmailClaim = "coach:github_verified_email";
    public const string AvatarClaim = "coach:github_avatar";
}
