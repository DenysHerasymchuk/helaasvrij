using DotNetEnv;
using InternshipCoach.Application;
using InternshipCoach.Infrastructure;
using InternshipCoach.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables("COACH_");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

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
    return;
}

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
app.UseAuthorization();
app.MapControllers();

app.Run();
