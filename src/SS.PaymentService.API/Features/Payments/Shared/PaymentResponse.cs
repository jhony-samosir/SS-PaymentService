using System;

namespace SS.PaymentService.API.Features.Payments.Shared;

public record PaymentResponse(
    Guid Id,
    Guid OrderPublicId,
    int UserId,
    Guid UserPublicId,
    decimal Amount,
    string Currency,
    string PaymentMethod,
    string PaymentStatus,
    string? PaymentReference,
    string? SnapToken,
    string? SnapRedirectUrl,
    DateTimeOffset CreatedAt
);
