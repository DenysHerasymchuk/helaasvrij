using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Auth.GitHubSignIn;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Auth.Login;
using InternshipCoach.Application.Auth.Me;
using InternshipCoach.Application.Auth.Register;
using InternshipCoach.Application.Documents.DeleteDocument;
using InternshipCoach.Application.Documents.GetDocument;
using InternshipCoach.Application.Documents.ListDocuments;
using InternshipCoach.Application.Documents.TailorCv;
using InternshipCoach.Application.Documents.UpdateDocument;
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
        services.AddScoped<TailorCvHandler>();
        services.AddScoped<ListDocumentsHandler>();
        services.AddScoped<GetDocumentHandler>();
        services.AddScoped<UpdateDocumentHandler>();
        services.AddScoped<DeleteDocumentHandler>();
        return services;
    }
}
