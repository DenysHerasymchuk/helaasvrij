using InternshipCoach.Application.Abstractions.Llm;
using InternshipCoach.Application.Auth.GitHubSignIn;
using InternshipCoach.Application.Llm;
using InternshipCoach.Application.Auth.Login;
using InternshipCoach.Application.Auth.Me;
using InternshipCoach.Application.Auth.Register;
using InternshipCoach.Application.Postings;
using InternshipCoach.Application.Postings.Add;
using InternshipCoach.Application.Postings.Browse;
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
        services.AddScoped<PostingExtractor>();
        services.AddScoped<PostingImporter>();
        services.AddScoped<AddPostingFromPasteHandler>();
        services.AddScoped<AddPostingFromUrlHandler>();
        services.AddScoped<ListPostingsHandler>();
        services.AddScoped<GetPostingHandler>();
        services.AddScoped<UpdatePostingDatesHandler>();
        services.AddScoped<DeletePostingHandler>();
        return services;
    }
}
