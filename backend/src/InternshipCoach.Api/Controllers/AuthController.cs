using System.Globalization;
using System.Security.Claims;
using InternshipCoach.Api.Auth;
using InternshipCoach.Application.Auth;
using InternshipCoach.Application.Auth.GitHubSignIn;
using InternshipCoach.Application.Auth.Login;
using InternshipCoach.Application.Auth.Me;
using InternshipCoach.Application.Auth.Register;
using InternshipCoach.Contracts.Auth;
using InternshipCoach.Domain.Users;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InternshipCoach.Api.Controllers;

[Route("api/auth")]
[ApiController]
public sealed class AuthController(IAuthenticationSchemeProvider schemes) : ControllerBase
{
    private const string DefaultReturnUrl = "/dashboard";
    private const string GitHubErrorUrl = "/login?error=github";

    // shortcut: no email verification or password reset; add both before real users sign up with email.
    [HttpPost("register")]
    [Consumes("application/json")]
    [EnableRateLimiting(AuthSchemes.RateLimitPolicy)]
    public async Task<IActionResult> Register(
        RegisterRequest request, [FromServices] RegisterHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new RegisterCommand(request.Name, request.Email, request.Password), cancellationToken);

        switch (result.Status)
        {
            case AuthStatus.Ok:
                await SignInAsync(result.User!, isPersistent: false);
                return Created("/api/auth/me", ToMe(result.User!));
            case AuthStatus.Invalid:
                return ValidationProblem(new ValidationProblemDetails(
                    result.Errors!.ToDictionary(pair => pair.Key, pair => pair.Value)));
            default:
                return Problem(statusCode: StatusCodes.Status409Conflict, title: "An account with this email already exists.");
        }
    }

    [HttpPost("login")]
    [Consumes("application/json")]
    [EnableRateLimiting(AuthSchemes.RateLimitPolicy)]
    public async Task<IActionResult> Login(
        LoginRequest request, [FromServices] LoginHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new LoginCommand(request.Email, request.Password), cancellationToken);

        if (!result.Succeeded)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Email or password is incorrect.");
        }

        await SignInAsync(result.User!, request.RememberMe);
        return Ok(ToMe(result.User!));
    }

    [HttpPost("logout")]
    [Consumes("application/json")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Cookie);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<MeResponse>> Me([FromServices] MeHandler handler, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Unauthorized();
        }

        var user = await handler.HandleAsync(userId, cancellationToken);
        if (user is null)
        {
            // The account was deleted: drop the stale cookie.
            await HttpContext.SignOutAsync(AuthSchemes.Cookie);
            return Unauthorized();
        }

        return Ok(ToMe(user));
    }

    [HttpGet("github")]
    public async Task<IActionResult> GitHub(string? returnUrl)
    {
        if (await schemes.GetSchemeAsync(AuthSchemes.GitHub) is null)
        {
            return Redirect(GitHubErrorUrl); // GitHub keys are not configured
        }

        var complete = $"/api/auth/github/complete?returnUrl={Uri.EscapeDataString(SafeReturnUrl(returnUrl))}";
        return Challenge(new AuthenticationProperties { RedirectUri = complete }, AuthSchemes.GitHub);
    }

    [HttpGet("github/complete")]
    public async Task<IActionResult> GitHubComplete(
        string? returnUrl, [FromServices] GitHubSignInHandler handler, CancellationToken cancellationToken)
    {
        var external = await HttpContext.AuthenticateAsync(AuthSchemes.External);
        await HttpContext.SignOutAsync(AuthSchemes.External);

        var principal = external.Principal;
        if (!external.Succeeded
            || principal is null
            || !long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var gitHubId))
        {
            return Redirect(GitHubErrorUrl);
        }

        var command = new GitHubSignInCommand(
            gitHubId,
            principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            principal.FindFirstValue("urn:github:name"),
            principal.FindFirstValue(AuthSchemes.VerifiedEmailClaim),
            principal.FindFirstValue(AuthSchemes.AvatarClaim));

        var result = await handler.HandleAsync(command, cancellationToken);
        if (!result.Succeeded)
        {
            return Redirect(GitHubErrorUrl);
        }

        await SignInAsync(result.User!, isPersistent: false);
        return Redirect(SafeReturnUrl(returnUrl));
    }

    private Task SignInAsync(User user, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Email, user.Email.Value)
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.Cookie));

        // Not persistent = session cookie (gone when the browser closes).
        var properties = new AuthenticationProperties { IsPersistent = isPersistent };
        if (isPersistent)
        {
            properties.ExpiresUtc = DateTimeOffset.UtcNow.Add(AuthExtensions.RememberMeLifetime);
        }

        return HttpContext.SignInAsync(AuthSchemes.Cookie, principal, properties);
    }

    // Only local paths, so a crafted link cannot bounce the user to another site after sign-in.
    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : DefaultReturnUrl;

    private static MeResponse ToMe(User user) =>
        new(user.Id, user.Email.Value, user.DisplayName, user.AvatarUrl, user.GitHubLogin);
}
