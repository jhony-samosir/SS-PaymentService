using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SS.PaymentService.API.Domain.Entities;
using SS.PaymentService.API.Domain.Enums;
using SS.PaymentService.API.Infrastructure.Data;
using SS.PaymentService.API.Infrastructure.Gateway;

namespace SS.PaymentService.API.Features.Payments.CancelPayment;

public class CancelPaymentHandler(
    ApplicationDbContext dbContext,
    MidtransClient midtransClient,
    ILogger<CancelPaymentHandler> logger
) : IRequestHandler<CancelPaymentCommand, IResult>
{
    public async Task<IResult> Handle(CancelPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.PublicId == request.PaymentPublicId, cancellationToken);

        if (payment == null)
        {
            return Results.NotFound(new { Message = $"Payment with ID {request.PaymentPublicId} not found." });
        }

        if (payment.PaymentStatus == PaymentStatus.Paid || payment.PaymentStatus == PaymentStatus.Refunded)
        {
            return Results.BadRequest(new { Message = $"Payment in state '{payment.PaymentStatus}' cannot be cancelled." });
        }

        try
        {
            var cancelled = await midtransClient.CancelTransactionAsync(payment.PublicId.ToString());
            if (!cancelled)
            {
                return Results.BadRequest(new { Message = "Failed to cancel transaction on Midtrans gateway." });
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Midtrans Cancel API failed for payment {PaymentPublicId}", payment.PublicId);
            // We can continue to cancel locally or return error. Let's return error for consistency.
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

        payment.PaymentStatus = PaymentStatus.Cancelled;
        payment.UpdatedAt = DateTimeOffset.UtcNow;
        payment.UpdatedBy = "APICancel";

        // Store outbox event
        var cancelPayload = new
        {
            eventId = Guid.NewGuid().ToString(),
            orderPublicId = payment.OrderPublicId,
            paymentPublicId = payment.PublicId,
            cancelledAt = DateTimeOffset.UtcNow
        };

        var outbox = new OutboxEvent
        {
            EventType = "payment.cancelled",
            AggregateType = "Payment",
            AggregateId = payment.PublicId.ToString(),
            Payload = JsonSerializer.Serialize(cancelPayload),
            Status = "PENDING"
        };

        dbContext.OutboxEvents.Add(outbox);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { Message = "Payment cancelled successfully.", PaymentId = payment.PublicId });
    }
}
