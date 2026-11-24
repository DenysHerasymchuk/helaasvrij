using InternshipCoach.Application.Abstractions;
using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Infrastructure.Llm;
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
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IAccountDeleter, AccountDeleter>();

        var route33 = configuration.GetSection(Route33Options.SectionName).Get<Route33Options>() ?? new Route33Options();
        if (string.IsNullOrWhiteSpace(route33.BaseUrl))
        {
            route33.BaseUrl = Route33Options.DefaultBaseUrl;
        }

        services.AddSingleton(route33);
        services.AddSingleton(new LlmLimits(configuration.GetValue("Llm:DailyCallCap", 200)));
        services.AddSingleton<ILlmTransport>(_ => new Route33LlmTransport(route33));
        services.AddScoped<ILlmCallLog, LlmCallLog>();

        services.AddDataProtection()
            .SetApplicationName("InternshipCoach")
            .PersistKeysToDbContext<InternshipCoachDbContext>();

        return services;
    }
}
