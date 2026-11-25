namespace InternshipCoach.Contracts.Jobs;

public sealed record JobResponse(
    string Id, string Title, string? Company, string? Location, string? Description, string? Created, string Url,
    decimal? SalaryMin, decimal? SalaryMax, bool SalaryIsPredicted, string? ContractType, string? ContractTime, string? Category);

public sealed record JobPostingRequest(string? JobId);
