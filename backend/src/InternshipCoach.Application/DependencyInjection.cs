using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Auth.GitHubSignIn;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Auth.Login;
using InternshipCoach.Application.Auth.Me;
using InternshipCoach.Application.Auth.Register;
using InternshipCoach.Application.Sources.Cv;
using InternshipCoach.Application.Sources.Delete;
using InternshipCoach.Application.Sources.GitHub;
using InternshipCoach.Application.Sources.List;
using InternshipCoach.Application.Sources.Site;
using InternshipCoach.Application.Sources.Text;
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
        services.AddScoped<ILlmClient, LlmClient>();
        services.AddScoped<SyncGitHubHandler>();
        services.AddScoped<UploadCvHandler>();
        services.AddScoped<AddTextHandler>();
        services.AddScoped<AddSiteHandler>();
        services.AddScoped<ListSourcesHandler>();
        services.AddScoped<DeleteSourceHandler>();
        return services;
    }
}
