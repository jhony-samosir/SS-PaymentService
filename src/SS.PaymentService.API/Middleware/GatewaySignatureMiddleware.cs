using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SS.PaymentService.API.Middleware;

/// <summary>
/// Middleware to verify that the request originated from the SS-APIGateway.
/// Utilizes HMAC-SHA256 signature verification over "{method}:{path}:{timestamp}".
/// </summary>
public sealed class GatewaySignatureMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GatewaySignatureMiddleware> _logger;
    private readonly byte[] _keyBytes;
    private readonly string _headerName;
    private const int TimestampWindowSeconds = 30;

    public GatewaySignatureMiddleware(RequestDelegate next, IConfiguration config, ILogger<GatewaySignatureMiddleware> logger)
    {
        _next = next;
        _logger = logger;

        var secretEnvVar = config["InternalSignature:SecretKeyEnvVar"] ?? "GATEWAY_HMAC_SECRET";
        var secret = Environment.GetEnvironmentVariable(secretEnvVar);
        _headerName = config["InternalSignature:HeaderName"] ?? "X-Internal-Signature";

        if (string.IsNullOrWhiteSpace(secret))
        {
            // Fallback for development if environment variable is not set yet
            secret = "dev_secret_key_change_in_production";
            _logger.LogWarning("HMAC secret '{EnvVar}' not found in environment. Using fallback development secret.", secretEnvVar);
        }

        _keyBytes = Encoding.UTF8.GetBytes(secret);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        
        // Bypass signature check for local health endpoint, API Docs, and Midtrans webhook callback
        if (path.StartsWithSegments("/health") || 
            path.StartsWithSegments("/openapi") || 
            path.StartsWithSegments("/api/payments/webhook"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(_headerName, out var signatureValues) ||
            !context.Request.Headers.TryGetValue("X-Gateway-Timestamp", out var timestampValues))
        {
            _logger.LogWarning("Rejecting request: Missing gateway signature or timestamp headers for path {Path}", path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { Message = "Unauthorized: Missing origin verification." });
            return;
        }

        var incomingSignature = signatureValues.ToString();
        var incomingTimestampStr = timestampValues.ToString();

        if (!long.TryParse(incomingTimestampStr, out var timestamp))
        {
            _logger.LogWarning("Rejecting request: Invalid timestamp format '{TimestampStr}'", incomingTimestampStr);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { Message = "Unauthorized: Invalid origin payload." });
            return;
        }

        // Validate timestamp freshness (Replay attack protection - 30 seconds window)
        var utcNowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(utcNowUnix - timestamp) > TimestampWindowSeconds)
        {
            _logger.LogWarning("Rejecting request: Timestamp expired/invalid. Gateway: {GatewayTs}, Service: {ServiceTs}, Delta: {Delta}s", 
                timestamp, utcNowUnix, utcNowUnix - timestamp);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { Message = "Unauthorized: Verification timestamp expired." });
            return;
        }

        // Recompute the HMAC signature
        var method = context.Request.Method;
        var fullPathAndQuery = context.Request.Path + context.Request.QueryString;
        var payload = $"{method}:{fullPathAndQuery}:{incomingTimestampStr}";

        var calculatedSignature = ComputeHmac(payload);

        // Constant-time comparison to prevent timing attacks
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(calculatedSignature), 
            Encoding.UTF8.GetBytes(incomingSignature)))
        {
            _logger.LogWarning("Rejecting request: Signature mismatch for path {Path}. Expected: {Expected}, Received: {Received}", 
                path, calculatedSignature, incomingSignature);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { Message = "Unauthorized: Origin signature verification failed." });
            return;
        }

        // Request validated, proceed to pipeline
        await _next(context);
    }

    private string ComputeHmac(string payload)
    {
        using var hmac = new HMACSHA256(_keyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToBase64String(hash);
    }
}
