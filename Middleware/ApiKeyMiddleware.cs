using System.Text.Json;
using BaaS.Services;

namespace BaaS.Middleware;

public class ApiKeyMiddleware
{
    private const string ApiKeyHeaderName = "x-api-key";
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApiKeyAuthService apiKeyAuthService)
    {
        if (IsPublicPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var apiKey = context.Request.Headers[ApiKeyHeaderName].FirstOrDefault();
        var authResult = await apiKeyAuthService.EvaluateAsync(apiKey, context.Request.Method, context.RequestAborted);

        if (!authResult.IsAuthorized)
        {
            if (authResult.IsForbidden)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Forbidden" }));
                return;
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Unauthorized" }));
            return;
        }

        context.Items["ApiRole"] = authResult.Role;
        context.Items["ApiUserId"] = authResult.UserId;
        context.Items["ApiUserEmail"] = authResult.Email;
        _logger.LogDebug("Request authorized with role {Role}.", authResult.Role);

        await _next(context);
    }

    private static bool IsPublicPath(PathString path)
    {
        return path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/api/auth/register", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.Value, "/", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.Value, "/index.html", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.Value, "/styles.css", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.Value, "/app.js", StringComparison.OrdinalIgnoreCase)
            || string.Equals(path.Value, "/favicon.ico", StringComparison.OrdinalIgnoreCase);
    }
}
