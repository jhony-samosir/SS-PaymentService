using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SS.PaymentService.API.Infrastructure.Data;
using Xunit;

namespace SS.PaymentService.Tests;

public class GatewaySignatureMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GatewaySignatureMiddlewareTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Production"); // Enable middleware (since it is bypassed in Testing mode)
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
                if (descriptor != null) services.Remove(descriptor);

                var dbContextDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ApplicationDbContext));
                if (dbContextDescriptor != null) services.Remove(dbContextDescriptor);

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("GatewaySignatureMiddlewareTestsDb");
                });
            });
        });
    }

    [Fact]
    public async Task Request_WithoutSignature_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsync("/api/payments", new StringContent("{}", Encoding.UTF8, "application/json"));

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_ToBypassedPath_ReturnsOkOrBadRequest()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act & Assert for health
        var healthRes = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, healthRes.StatusCode);

        // Act & Assert for webhook (returns bad request due to missing webhook payload/signature, not 401 Unauthorized!)
        var webhookRes = await client.PostAsync("/api/payments/webhook/midtrans", new StringContent("{}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, webhookRes.StatusCode);
    }

    [Fact]
    public async Task Request_WithValidSignature_BypassesMiddleware()
    {
        // Arrange
        var client = _factory.CreateClient();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var secret = "dev_secret_key_change_in_production"; // Fallback default
        var method = "POST";
        var path = "/api/payments";
        var payload = $"{method}:{path}:{timestamp}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var signature = Convert.ToBase64String(hash);

        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        request.Headers.Add("X-Internal-Signature", signature);
        request.Headers.Add("X-Gateway-Timestamp", timestamp);

        // Act
        var response = await client.SendAsync(request);

        // Assert (should NOT be 401 Unauthorized, but instead 400 Bad Request or 201 Created depending on payload validation)
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
