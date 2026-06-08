using System;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using Ganss.Xss;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SS.PaymentService.API.Domain.Entities;
using SS.PaymentService.API.Domain.Enums;
using SS.PaymentService.API.Features.Payments.Shared;
using SS.PaymentService.API.Infrastructure.Data;
using SS.PaymentService.API.Infrastructure.Gateway;

namespace SS.PaymentService.API.Features.Payments.InitiatePayment;

public class InitiatePaymentHandler(
    ApplicationDbContext dbContext,
    IValidator<InitiatePaymentCommand> validator,
    MidtransClient midtransClient,
    ILogger<InitiatePaymentHandler> logger
) : IRequestHandler<InitiatePaymentCommand, IResult>
{
    private static readonly HtmlSanitizer Sanitizer = new();

    public async Task<IResult> Handle(InitiatePaymentCommand command, CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var existingPayment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.OrderPublicId == command.OrderPublicId, cancellationToken);

        if (existingPayment != null)
        {
            if (existingPayment.PaymentStatus == PaymentStatus.Paid)
            {
                return Results.Conflict(new { Message = $"Payment for Order (Id: {command.OrderPublicId}) has already been paid." });
            }

            return Results.Ok(PaymentMapper.MapToResponse(existingPayment));
        }

        var sanitizedCurrency = Sanitizer.Sanitize(command.Currency);
        var sanitizedPaymentMethod = Sanitizer.Sanitize(command.PaymentMethod);

        var payment = new Payment
        {
            OrderPublicId = command.OrderPublicId,
            UserId = command.UserId,
            UserPublicId = command.UserPublicId,
            Amount = command.Amount,
            Currency = sanitizedCurrency,
            PaymentMethod = sanitizedPaymentMethod,
            PaymentStatus = PaymentStatus.Pending,
            CreatedBy = "APIInitiate"
        };

        try
        {
            var snapRes = await midtransClient.CreateSnapTransactionAsync(payment.PublicId, payment.Amount, payment.Currency);
            if (snapRes != null)
            {
                payment.SnapToken = snapRes.Token;
                payment.SnapRedirectUrl = snapRes.RedirectUrl;
                payment.PaymentStatus = PaymentStatus.WaitingPayment;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate Midtrans Snap Token for order {OrderPublicId}", command.OrderPublicId);
        }

        dbContext.Payments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Created($"/api/payments/{payment.PublicId}", PaymentMapper.MapToResponse(payment));
    }
}
