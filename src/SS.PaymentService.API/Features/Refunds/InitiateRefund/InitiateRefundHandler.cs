using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SS.PaymentService.API.Domain.Entities;
using SS.PaymentService.API.Domain.Enums;
using SS.PaymentService.API.Infrastructure.Data;
using SS.PaymentService.API.Infrastructure.Gateway;

namespace SS.PaymentService.API.Features.Refunds.InitiateRefund;

public class InitiateRefundHandler(
    ApplicationDbContext dbContext,
    IValidator<InitiateRefundCommand> validator,
    MidtransClient midtransClient,
    ILogger<InitiateRefundHandler> logger
) : IRequestHandler<InitiateRefundCommand, IResult>
{
    public async Task<IResult> Handle(InitiateRefundCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var payment = await dbContext.Payments
            .Include(p => p.Refunds)
            .FirstOrDefaultAsync(p => p.PublicId == command.PaymentPublicId, cancellationToken);

        if (payment == null)
        {
            return Results.NotFound(new { Message = $"Payment with ID {command.PaymentPublicId} not found." });
        }

        if (payment.PaymentStatus != PaymentStatus.Paid && payment.PaymentStatus != PaymentStatus.Refunded)
        {
            return Results.BadRequest(new { Message = $"Payment in status '{payment.PaymentStatus}' cannot be refunded." });
        }

        var totalRefunded = payment.Refunds
            .Where(r => r.RefundStatus == RefundStatus.Success)
            .Sum(r => r.RefundAmount);

        var remainingAmount = payment.Amount - totalRefunded;

        if (command.RefundAmount > remainingAmount)
        {
            return Results.BadRequest(new { Message = $"Requested refund amount {command.RefundAmount} exceeds remaining refundable amount {remainingAmount}." });
        }

        var refund = new PaymentRefund
        {
            PaymentId = payment.Id,
            RefundAmount = command.RefundAmount,
            RefundReason = command.RefundReason,
            RefundStatus = RefundStatus.Pending,
            CreatedBy = "APIRefund"
        };

        dbContext.Refunds.Add(refund);
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var midtransRes = await midtransClient.RefundTransactionAsync(payment.PublicId.ToString(), refund.RefundAmount, refund.RefundReason ?? "No reason");
            if (midtransRes != null && (midtransRes.StatusCode == "200" || midtransRes.StatusCode == "201"))
            {
                refund.RefundStatus = RefundStatus.Success;
                refund.RefundReference = midtransRes.RefundChargebackId;
                
                // If fully refunded, mark status as Refunded
                if (refund.RefundAmount == remainingAmount)
                {
                    payment.PaymentStatus = PaymentStatus.Refunded;
                }
                else
                {
                    // Partial refund - keep status as Paid but we can also use Paid or a specific flag if needed
                }

                // Write outbox event
                var refundEvent = new
                {
                    eventId = Guid.NewGuid().ToString(),
                    paymentPublicId = payment.PublicId,
                    orderPublicId = payment.OrderPublicId,
                    refundPublicId = refund.PublicId,
                    refundAmount = refund.RefundAmount,
                    totalRefundedAmount = totalRefunded + refund.RefundAmount,
                    refundReference = refund.RefundReference,
                    refundedAt = DateTimeOffset.UtcNow
                };

                var outbox = new OutboxEvent
                {
                    EventType = "payment.refunded",
                    AggregateType = "PaymentRefund",
                    AggregateId = refund.PublicId.ToString(),
                    Payload = JsonSerializer.Serialize(refundEvent),
                    Status = "PENDING"
                };

                dbContext.OutboxEvents.Add(outbox);
            }
            else
            {
                refund.RefundStatus = RefundStatus.Failed;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Midtrans Refund API failed for payment {PaymentPublicId}", payment.PublicId);
            refund.RefundStatus = RefundStatus.Failed;
            refund.UpdatedBy = "SystemError";
            refund.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.StatusCode(StatusCodes.Status502BadGateway);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new
        {
            Message = "Refund processed successfully.",
            RefundId = refund.PublicId,
            Status = refund.RefundStatus.ToString(),
            RefundAmount = refund.RefundAmount,
            PaymentStatus = payment.PaymentStatus.ToString()
        });
    }
}
