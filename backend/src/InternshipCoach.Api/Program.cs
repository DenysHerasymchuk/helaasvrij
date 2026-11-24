using InternshipCoach.Application.Auth.Register;
using DotNetEnv;
using InternshipCoach.Api.Auth;
using InternshipCoach.Application;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Infrastructure;
using InternshipCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("COACH_");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddCoachForwardedHeaders();
builder.Services.AddCoachAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddCoachRateLimiting(builder.Configuration);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy("DefaultCorsPolicy", policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

// Run once before starting the app (Docker entrypoint or CI): `dotnet InternshipCoach.Api.dll --migrate`.
if (args.Contains("--migrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<InternshipCoachDbContext>().Database.MigrateAsync();

    // shortcut: fixed dev test user (lotte@demo.test / Demo1234!), Development only; remove before real users.
    // Re-running is safe: the handler returns EmailTaken when the user already exists.
    if (app.Environment.IsDevelopment())
    {
        await scope.ServiceProvider.GetRequiredService<RegisterHandler>()
            .HandleAsync(new RegisterCommand("Lotte Peeters", "lotte@demo.test", "Demo1234!"), CancellationToken.None);

        await DemoDataSeeder.SeedPostingsAsync(
            scope.ServiceProvider.GetRequiredService<DbContextOptions<InternshipCoachDbContext>>(),
            scope.ServiceProvider.GetRequiredService<IClock>(), "lotte@demo.test", CancellationToken.None);
    }

    return;
}

app.UseForwardedHeaders();
app.UseMiddleware<UnauthorizedExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!app.Configuration.GetValue<bool>("Hosting:HttpsTerminatedAtProxy"))
{
    app.UseHttpsRedirection();
}

app.UseCors("DefaultCorsPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
