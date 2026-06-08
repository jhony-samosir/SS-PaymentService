using System;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace SS.PaymentService.API.Features.Payments.InitiatePayment;

public record InitiatePaymentCommand(
    Guid OrderPublicId,
    int UserId,
    Guid UserPublicId,
    decimal Amount,
    string Currency,
    string PaymentMethod
) : IRequest<IResult>;
