namespace InternshipCoach.Application.Profile;

/// <summary>The caller asked for something that cannot work (no sources yet). Maps to HTTP 400.</summary>
public sealed class ProfileInputException(string message) : Exception(message);
