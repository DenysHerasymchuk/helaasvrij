using System.Net.Mail;
using InternshipCoach.Domain.Common;

namespace InternshipCoach.Domain.Users;

// Self-validating: trimmed and lower-cased, so equality and the unique index are case-insensitive.
public sealed class Email : IEquatable<Email>
{
    public const int MaxLength = 254;

    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string? raw) =>
        TryCreate(raw, out var email) ? email : throw new DomainException("Email is not valid.");

    public static bool TryCreate(string? raw, out Email email)
    {
        email = null!;
        var value = raw?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(value) || value.Length > MaxLength)
        {
            return false;
        }

        if (!MailAddress.TryCreate(value, out var parsed) || parsed.Address != value || !value.Contains('@'))
        {
            return false;
        }

        email = new Email(value);
        return true;
    }

    public bool Equals(Email? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => Equals(obj as Email);

    public override int GetHashCode() => Value.GetHashCode();

    public override string ToString() => Value;
}
