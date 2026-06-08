using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using SS.PaymentService.API.Infrastructure.Data;
using SS.PaymentService.API.Features.Payments.InitiatePayment;
using SS.PaymentService.API.Features.Payments.Shared;
using Xunit;

namespace SS.PaymentService.Tests;

public class PaymentEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public PaymentEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Environment", "Testing"); // Disables signature middleware
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
                    options.UseInMemoryDatabase("PaymentEndpointsTestsDb");
                });
            });
        });
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(content);
        Assert.Equal("Healthy", content!.Status);
        Assert.Equal("PaymentService", content.Service);
    }

    [Fact]
    public async Task InitiatePayment_WithValidData_ReturnsCreated()
    {
        // Arrange
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();
        var request = new InitiatePaymentRequest(
            OrderPublicId: orderId,
            UserId: 1,
            UserPublicId: Guid.NewGuid(),
            Amount: 250000,
            Currency: "IDR",
            PaymentMethod: "Midtrans"
        );

        // Act
        var response = await client.PostAsJsonAsync("/api/payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PaymentResponse>();
        Assert.NotNull(created);
        Assert.Equal(orderId, created!.OrderPublicId);
        Assert.Equal(250000, created.Amount);
    }

    [Fact]
    public async Task GetPaymentByOrder_NotFound_ReturnsNotFound()
    {
        // Arrange
        var client = _factory.CreateClient();
        var orderId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/api/payments/order/{orderId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record HealthResponse(string Status, string Service);
}
