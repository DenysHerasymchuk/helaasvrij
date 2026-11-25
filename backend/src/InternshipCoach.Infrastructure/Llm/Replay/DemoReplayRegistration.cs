using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Llm;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace InternshipCoach.Infrastructure.Llm.Replay;

public static class DemoReplayRegistration
{
    /// <summary>
    /// Wraps the registered <see cref="ILlmClient"/> in <see cref="ReplayLlmClient"/> only when Demo:Replay is on.
    /// Call after the application layer registered <see cref="LlmClient"/>. With the flag off this does nothing.
    /// </summary>
    public static IServiceCollection AddDemoReplay(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(DemoReplayOptions.SectionName).Get<DemoReplayOptions>() ?? new DemoReplayOptions();
        if (!options.Replay)
        {
            return services;
        }

        var existing = services.LastOrDefault(d => d.ServiceType == typeof(ILlmClient))
            ?? throw new InvalidOperationException("Demo replay needs an ILlmClient registered first.");
        if (existing.ImplementationType != typeof(LlmClient))
        {
            throw new InvalidOperationException("Demo replay can only wrap the default LlmClient.");
        }

        services.Remove(existing);
        services.AddScoped<LlmClient>();
        services.AddSingleton(options);
        services.AddSingleton(new ReplayStore(options.Path));
        services.AddScoped<ILlmClient>(provider => new ReplayLlmClient(
            provider.GetRequiredService<LlmClient>(), provider.GetRequiredService<ReplayStore>(), options));
        return services;
    }
}
