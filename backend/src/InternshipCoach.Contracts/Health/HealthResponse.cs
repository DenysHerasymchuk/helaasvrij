namespace InternshipCoach.Contracts.Health;

public sealed record HealthResponse(string Service, string Status, bool DatabaseReachable, DateTimeOffset Timestamp);
