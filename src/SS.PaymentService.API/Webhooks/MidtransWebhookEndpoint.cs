using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SS.PaymentService.API.Domain.Entities;
using SS.PaymentService.API.Domain.Enums;
using SS.PaymentService.API.Infrastructure.Data;
using SS.PaymentService.API.Infrastructure.Gateway;

namespace SS.PaymentService.API.Webhooks;

public static class MidtransWebhookEndpoint
{
    public static IEndpointRouteBuilder MapMidtransWebhookEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/payments/webhook/midtrans", async (
            MidtransWebhookPayload payload,
            ApplicationDbContext dbContext,
            IOptions<MidtransOptions> midtransOptions,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("MidtransWebhook");
            logger.LogInformation("Received Midtrans Webhook for Order ID: {OrderId}, Status: {Status}", payload.OrderId, payload.TransactionStatus);

            var options = midtransOptions.Value;

            // Validate signature
            var isValid = MidtransSignatureValidator.Validate(
                payload.OrderId,
                payload.StatusCode,
                payload.GrossAmount,
                options.ServerKey,
                payload.SignatureKey
            );

            if (!isValid)
            {
                logger.LogWarning("Invalid Midtrans Signature for Order ID: {OrderId}", payload.OrderId);
                return Results.BadRequest(new { Message = "Invalid signature key." });
            }

            if (!Guid.TryParse(payload.OrderId, out var paymentPublicId))
            {
                logger.LogWarning("Invalid payment UUID format: {OrderId}", payload.OrderId);
                return Results.BadRequest(new { Message = "Invalid OrderId format." });
            }

            var payment = await dbContext.Payments
                .FirstOrDefaultAsync(p => p.PublicId == paymentPublicId);

            if (payment == null)
            {
                logger.LogWarning("Payment not found for order id: {OrderId}", payload.OrderId);
                return Results.NotFound(new { Message = "Payment transaction not found." });
            }

            var originalStatus = payment.PaymentStatus;
            var newStatus = MapMidtransStatus(payload.TransactionStatus);

            payment.PaymentStatus = newStatus;
            payment.PaymentReference = payload.TransactionId;
            payment.PaymentMethod = payload.PaymentType ?? payment.PaymentMethod;
            payment.UpdatedAt = DateTimeOffset.UtcNow;
            payment.UpdatedBy = "MidtransWebhook";

            // If the payment transitioned to Paid, record an outbox event
            if (newStatus == PaymentStatus.Paid && originalStatus != PaymentStatus.Paid)
            {
                var eventId = Guid.NewGuid();
                var paymentCompletedEvent = new
                {
                    eventId = eventId.ToString(),
                    orderPublicId = payment.OrderPublicId,
                    paymentReference = payment.PaymentReference,
                    amount = payment.Amount,
                    paymentMethod = payment.PaymentMethod,
                    completedAt = DateTimeOffset.UtcNow
                };

                var outbox = new OutboxEvent
                {
                    PublicId = eventId,
                    EventType = "payment.completed",
                    AggregateType = "Payment",
                    AggregateId = payment.PublicId.ToString(),
                    Payload = JsonSerializer.Serialize(paymentCompletedEvent),
                    Status = "PENDING"
                };

                dbContext.OutboxEvents.Add(outbox);
                logger.LogInformation("Payment {PaymentId} marked as PAID. Outbox event created.", payment.PublicId);
            }

            await dbContext.SaveChangesAsync();

            return Results.Ok(new { Status = "Success" });
        })
        .WithName("MidtransWebhook")
        .WithOpenApi();

        return app;
    }

    private static PaymentStatus MapMidtransStatus(string transactionStatus)
    {
        return transactionStatus.ToLower() switch
        {
            "capture" or "settlement" => PaymentStatus.Paid,
            "pending" => PaymentStatus.WaitingPayment,
            "deny" or "cancel" => PaymentStatus.Cancelled,
            "expire" => PaymentStatus.Expired,
            "refund" or "partial_refund" => PaymentStatus.Refunded,
            _ => PaymentStatus.Failed
        };
    }
}

public class MidtransWebhookPayload
{
    [JsonPropertyName("order_id")]
    public string OrderId { get; set; } = string.Empty;

    [JsonPropertyName("status_code")]
    public string StatusCode { get; set; } = string.Empty;

    [JsonPropertyName("gross_amount")]
    public string GrossAmount { get; set; } = string.Empty;

    [JsonPropertyName("signature_key")]
    public string SignatureKey { get; set; } = string.Empty;

    [JsonPropertyName("transaction_status")]
    public string TransactionStatus { get; set; } = string.Empty;

    [JsonPropertyName("transaction_id")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonPropertyName("payment_type")]
    public string? PaymentType { get; set; }

    [JsonPropertyName("status_message")]
    public string? StatusMessage { get; set; }
}
