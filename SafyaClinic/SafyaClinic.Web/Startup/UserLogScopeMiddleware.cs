using System.Security.Claims;

namespace SafyaClinic.Web.Startup;

/// <summary>
/// Adds the signed-in user's id to every log entry written while handling the request (F3a).
/// The request id is already added by ASP.NET Core.
/// </summary>
public sealed class UserLogScopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserLogScopeMiddleware> _logger;

    public UserLogScopeMiddleware(RequestDelegate next, ILogger<UserLogScopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            await _next(context);
            return;
        }

        using (_logger.BeginScope(new Dictionary<string, object> { ["UserId"] = userId }))
        {
            await _next(context);
        }
    }
}