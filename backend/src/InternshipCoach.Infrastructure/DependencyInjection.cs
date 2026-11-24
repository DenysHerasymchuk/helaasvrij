using InternshipCoach.Application.Abstractions;
using InternshipCoach.Infrastructure.Auth;
using InternshipCoach.Infrastructure.Persistence;
using InternshipCoach.Infrastructure.Time;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InternshipCoach.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InternshipCoach")
            ?? throw new InvalidOperationException("Connection string 'InternshipCoach' was not found.");

        // Pin the history table to the "coach" schema: without this EF writes it to "public" but looks in "coach",
        // so a second --migrate tried to create the tables again.
        services.AddDbContext<InternshipCoachDbContext>(options => options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", InternshipCoachDbContext.SchemaName)));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddDataProtection()
            .SetApplicationName("InternshipCoach")
            .PersistKeysToDbContext<InternshipCoachDbContext>();

        return services;
    }
}
