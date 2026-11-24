using InternshipCoach.Application.Abstractions;
using InternshipCoach.Contracts.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

// Searches filter the shared Adzuna snapshot (see AdzunaJobSnapshot); requests never call Adzuna directly.
[ApiController]
[Authorize]
[Route("api/jobs")]
public sealed class JobsController(IJobSnapshot snapshot) : ControllerBase
{
    private const string Unavailable = "Job search is not set up or Adzuna did not answer. Try again later.";

    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? what, [FromQuery] string? where, CancellationToken ct)
    {
        var jobs = await snapshot.LoadAsync(ct);
        if (jobs is null) return Problem(title: Unavailable, statusCode: 503);

        var words = (what ?? "").ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w is not ("stage" or "internship" or "stagiair" or "intern")).ToArray();
        var city = where?.Trim().ToLowerInvariant();
        return Ok(jobs
            .Where(j => words.All(w => $"{j.Title} {j.Description} {j.Company}".ToLowerInvariant().Contains(w)))
            .Where(j => string.IsNullOrEmpty(city) || (j.Location ?? "").ToLowerInvariant().Contains(city))
            .Take(50)
            .Select(ToResponse));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken ct)
    {
        var jobs = await snapshot.LoadAsync(ct);
        if (jobs is null) return Problem(title: Unavailable, statusCode: 503);
        var job = jobs.FirstOrDefault(j => j.Id == id);
        return job is null ? Problem(title: "This job is no longer in the list. Search again.", statusCode: 404) : Ok(ToResponse(job));
    }

    private static JobResponse ToResponse(Job j) =>
        new(j.Id, j.Title, j.Company, j.Location, j.Description, j.Created, j.Url,
            j.SalaryMin, j.SalaryMax, j.SalaryIsPredicted, j.ContractType, j.ContractTime, j.Category);
}
