using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using System.Threading.Tasks;

namespace SS.PaymentService.API.Middleware;

/// <summary>
/// Enforces secure response headers for Defense-in-Depth compliance.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers[HeaderNames.XContentTypeOptions] = "nosniff";
            headers[HeaderNames.XFrameOptions] = "DENY";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
            headers[HeaderNames.StrictTransportSecurity] = "max-age=31536000; includeSubDomains; preload";

            // Security masking
            headers.Remove("X-Powered-By");
            headers.Remove("X-AspNet-Version");

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
