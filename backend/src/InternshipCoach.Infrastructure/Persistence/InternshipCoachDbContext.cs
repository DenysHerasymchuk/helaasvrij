using InternshipCoach.Domain.Users;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InternshipCoach.Infrastructure.Persistence;

public sealed class InternshipCoachDbContext(DbContextOptions<InternshipCoachDbContext> options)
    : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<User> Users => Set<User>();

    // Cookie-encryption keys live in the database so sessions survive an api container restart.
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("coach");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InternshipCoachDbContext).Assembly);
    }
}
