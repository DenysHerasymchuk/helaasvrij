namespace InternshipCoach.Application.Settings.Update;

public sealed record UpdateSettingsCommand(string? DefaultLanguage, int StrongMatchThreshold, bool EmailAlerts);
