using System.Security.Claims;
using System.Text.Encodings.Web;
using InternshipCoach.Application.Abstractions;
using InternshipCoach.Domain.Tokens;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace InternshipCoach.Api.Auth;

/// <summary>Signs a request in from `Authorization: Bearer hv_...` (personal access token). Same principal as the cookie.</summary>
public sealed class BearerTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IApiTokenRepository tokens, IClock clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        const string scheme = "Bearer ";
        if (!header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        var secret = header[scheme.Length..].Trim();
        if (!secret.StartsWith(ApiToken.Prefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("Unknown token.");
        }

        var userId = await tokens.ResolveAsync(ApiToken.HashOf(secret), clock.UtcNow, Context.RequestAborted);
        if (userId is null)
        {
            return AuthenticateResult.Fail("Unknown or revoked token.");
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())], AuthSchemes.Bearer);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), AuthSchemes.Bearer));
    }
}
