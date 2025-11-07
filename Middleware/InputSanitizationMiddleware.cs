using System.Text;
using System.Text.RegularExpressions;

namespace IoTDetectorApi.Middleware;

public class InputSanitizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<InputSanitizationMiddleware> _logger;

    // Regex patterns for detecting potential XSS attacks
    private static readonly Regex XssPattern = new(
        @"<script|javascript:|onerror=|onload=|<iframe|eval\(|<object|<embed",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Regex pattern for SQL injection attempts
    private static readonly Regex SqlPattern = new(
        @"(\bSELECT\b|\bINSERT\b|\bUPDATE\b|\bDELETE\b|\bDROP\b|\bCREATE\b|\bALTER\b|\bEXEC\b|\bUNION\b).*(\bFROM\b|\bWHERE\b|\bINTO\b)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public InputSanitizationMiddleware(RequestDelegate next, ILogger<InputSanitizationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check query string parameters
        foreach (var query in context.Request.Query)
        {
            var value = query.Value.ToString();
            if (ContainsMaliciousContent(value))
            {
                _logger.LogWarning(
                    "Potential malicious input detected in query parameter '{Key}' from IP {RemoteIp}",
                    query.Key,
                    context.Connection.RemoteIpAddress);

                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Invalid input detected",
                    message = "The request contains potentially malicious content"
                });
                return;
            }
        }

        // Check route values
        foreach (var route in context.Request.RouteValues)
        {
            var value = route.Value?.ToString() ?? string.Empty;
            if (ContainsMaliciousContent(value))
            {
                _logger.LogWarning(
                    "Potential malicious input detected in route parameter '{Key}' from IP {RemoteIp}",
                    route.Key,
                    context.Connection.RemoteIpAddress);

                context.Response.StatusCode = 400;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Invalid input detected",
                    message = "The request contains potentially malicious content"
                });
                return;
            }
        }

        await _next(context);
    }

    private static bool ContainsMaliciousContent(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        // Check for XSS patterns
        if (XssPattern.IsMatch(input))
            return true;

        // Check for SQL injection patterns
        if (SqlPattern.IsMatch(input))
            return true;

        // Check for path traversal attempts
        if (input.Contains("../") || input.Contains("..\\"))
            return true;

        // Check for null bytes
        if (input.Contains('\0'))
            return true;

        return false;
    }
}

public static class InputSanitizationMiddlewareExtensions
{
    public static IApplicationBuilder UseInputSanitization(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<InputSanitizationMiddleware>();
    }
}
