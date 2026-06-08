using System;

namespace SS.PaymentService.API.Features.Payments.InitiatePayment;

public record InitiatePaymentRequest(
    Guid OrderPublicId,
    int UserId,
    Guid UserPublicId,
    decimal Amount,
    string Currency,
    string PaymentMethod
);
