namespace InternshipCoach.Contracts.Settings;

public sealed record SettingsResponse(string DefaultLanguage, int StrongMatchThreshold, bool EmailAlerts);

public sealed record UpdateSettingsRequest(string? DefaultLanguage, int StrongMatchThreshold, bool EmailAlerts);

public sealed record DeleteAccountRequest(string? Confirm);
