namespace InternshipCoach.Application.Abstractions;

/// <summary>One Adzuna ad as stored in the shared snapshot. Public data, not owned by a user.</summary>
public sealed record Job(
    string Id, string Title, string? Company, string? Location, string? Description, string? Created, string Url,
    decimal? SalaryMin = null, decimal? SalaryMax = null, bool SalaryIsPredicted = false,
    string? ContractType = null, string? ContractTime = null, string? Category = null);

/// <summary>Shared snapshot of Adzuna internship ads, refreshed at most once a day.</summary>
public interface IJobSnapshot
{
    /// <summary>Null when job search is not set up and no snapshot exists yet.</summary>
    Task<IReadOnlyList<Job>?> LoadAsync(CancellationToken cancellationToken);
}
