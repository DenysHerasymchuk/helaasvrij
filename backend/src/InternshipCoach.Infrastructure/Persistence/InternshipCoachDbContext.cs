using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

public sealed class InternshipCoachDbContext(DbContextOptions<InternshipCoachDbContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("coach");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InternshipCoachDbContext).Assembly);
    }
}
