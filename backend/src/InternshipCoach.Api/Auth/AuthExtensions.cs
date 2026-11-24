using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using AspNet.Security.OAuth.GitHub;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace InternshipCoach.Api.Auth;

public static class AuthExtensions
{
    public static readonly TimeSpan RememberMeLifetime = TimeSpan.FromDays(30);

    public static IServiceCollection AddCoachAuthentication(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment environment)
    {
        var authentication = services.AddAuthentication(AuthSchemes.Cookie)
            .AddCookie(AuthSchemes.Cookie, options =>
            {
                ConfigureCookie(options, "coach.auth", environment);
                options.ExpireTimeSpan = RememberMeLifetime;
            })
            .AddCookie(AuthSchemes.External, options =>
            {
                // Short-lived holder for the GitHub identity between the callback and our own sign-in.
                ConfigureCookie(options, "coach.external", environment);
                options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
            });

        // GitHub sign-in is optional: without keys the app still runs with email and password.
        var clientId = configuration["GitHub:ClientId"];
        var clientSecret = configuration["GitHub:ClientSecret"];
        if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
        {
            authentication.AddGitHub(AuthSchemes.GitHub, options =>
            {
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.SignInScheme = AuthSchemes.External;
                options.CallbackPath = "/api/auth/github/callback";
                options.Scope.Add("read:user");
                options.Scope.Add("user:email");
                options.SaveTokens = false; // the access token is never stored
                // Lax is enough for a top-level redirect back from github.com and works over plain http.
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.ClaimActions.MapJsonKey(AuthSchemes.AvatarClaim, "avatar_url");
                options.Events.OnCreatingTicket = AddVerifiedEmailClaimAsync;
                options.Events.OnRemoteFailure = context =>
                {
                    context.Response.Redirect("/login?error=github");
                    context.HandleResponse();
                    return Task.CompletedTask;
                };
            });
        }

        services.AddAuthorization();
        return services;
    }

    // shortcut: in-memory limiter, so limits are per api instance; move to a shared store when running more than one.
    public static IServiceCollection AddCoachRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimit:AuthPerMinute", 10);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthSchemes.RateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    // shortcut: any private-network peer is trusted as the proxy; pin it to the nginx address in production.
    // Trust X-Forwarded-* only from loopback and private networks (the Docker network), not the internet.
    public static IServiceCollection AddCoachForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            foreach (var cidr in new[] { "127.0.0.0/8", "::1/128", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16" })
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidr));
            }
        });

        return services;
    }

    private static void ConfigureCookie(CookieAuthenticationOptions options, string name, IWebHostEnvironment environment)
    {
        options.Cookie.Name = name;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

        // The API is called by fetch: answer 401/403 instead of redirecting to a login page.
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    }

    // GitHub's /user only gives the public email. /user/emails says which addresses are verified.
    private static async Task AddVerifiedEmailClaimAsync(OAuthCreatingTicketContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user/emails");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        request.Headers.Authorization = new("Bearer", context.AccessToken);
        request.Headers.UserAgent.ParseAdd("InternshipCoach");

        using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
        string? verified = null;
        foreach (var item in json.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("verified", out var isVerified) || !isVerified.GetBoolean())
            {
                continue;
            }

            var address = item.GetProperty("email").GetString();
            if (item.TryGetProperty("primary", out var isPrimary) && isPrimary.GetBoolean())
            {
                verified = address;
                break;
            }

            verified ??= address;
        }

        if (verified is not null)
        {
            context.Identity?.AddClaim(new Claim(AuthSchemes.VerifiedEmailClaim, verified));
        }
    }
}
