using Microsoft.Extensions.DependencyInjection;

namespace InternshipCoach.Application;

public static class DependencyInjection
{
    // Use case handlers are registered here as features are added.
    public static IServiceCollection AddApplication(this IServiceCollection services) => services;
}
