using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Auth.GitHubSignIn;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Auth.Login;
using InternshipCoach.Application.Auth.Me;
using InternshipCoach.Application.Auth.Register;
using InternshipCoach.Application.Chat.Send;
using Microsoft.Extensions.DependencyInjection;

namespace InternshipCoach.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<RegisterHandler>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<GitHubSignInHandler>();
        services.AddScoped<MeHandler>();
        services.AddScoped<SendChatHandler>();
        services.AddScoped<ILlmClient, LlmClient>();
        return services;
    }
}
