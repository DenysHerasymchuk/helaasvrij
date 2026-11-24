using InternshipCoach.Application.Abstractions;
using Microsoft.AspNetCore.Identity;

namespace InternshipCoach.Infrastructure.Auth;

// Wraps the Identity hasher (PBKDF2, versioned format) without pulling in full ASP.NET Identity.
public sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();
    private readonly object _subject = new();

    public string Hash(string password) => _hasher.HashPassword(_subject, password);

    public bool Verify(string passwordHash, string password) =>
        _hasher.VerifyHashedPassword(_subject, passwordHash, password) != PasswordVerificationResult.Failed;
}
