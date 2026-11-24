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

        services.AddDbContext<InternshipCoachDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddDataProtection()
            .SetApplicationName("InternshipCoach")
            .PersistKeysToDbContext<InternshipCoachDbContext>();

        return services;
    }
}
