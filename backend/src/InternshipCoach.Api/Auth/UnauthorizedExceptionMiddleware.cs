namespace InternshipCoach.Api.Auth;

/// <summary>Turns <see cref="UnauthorizedAccessException" /> (for example from ICurrentUser) into a 401 response.</summary>
public sealed class UnauthorizedExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (UnauthorizedAccessException) when (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        }
    }
}
