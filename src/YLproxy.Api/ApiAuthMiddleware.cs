using System.Net;
using Microsoft.AspNetCore.Http;
using YLproxy.Infrastructure;

namespace YLproxy.Api;

public sealed class ApiAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _expectedToken;
    private readonly ILogger _logger;

    public ApiAuthMiddleware(RequestDelegate next, string expectedToken)
    {
        _next = next;
        _expectedToken = expectedToken;
        _logger = LogFactory.CreateLogger();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Allow health-check and swagger (no auth)
        if (context.Request.Path.StartsWithSegments("/api/health", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader))
        {
            _logger.Warn("API: request rejected — no Authorization header");
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"success\":false,\"error\":\"Missing Authorization header\"}");
            return;
        }

        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.Warn("API: request rejected — non-Bearer auth scheme");
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"success\":false,\"error\":\"Only Bearer authentication is supported\"}");
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        if (!string.Equals(token, _expectedToken, StringComparison.Ordinal))
        {
            _logger.Warn("API: request rejected — invalid token");
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"success\":false,\"error\":\"Invalid access token\"}");
            return;
        }

        await _next(context);
    }
}
