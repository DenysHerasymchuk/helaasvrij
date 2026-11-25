using InternshipCoach.Application.Abstractions;
using InternshipCoach.Contracts.Health;
using InternshipCoach.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace InternshipCoach.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class HealthController(InternshipCoachDbContext db, IClock clock, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var databaseReachable = await db.Database.CanConnectAsync(cancellationToken);

        return Ok(new HealthResponse(
            configuration["Service:Name"] ?? "Internship Coach API",
            databaseReachable ? "Healthy" : "Degraded",
            databaseReachable,
            clock.UtcNow));
    }
}
