using SS.PaymentService.API.Domain.Entities;

namespace SS.PaymentService.API.Features.Payments.Shared;

public static class PaymentMapper
{
    public static PaymentResponse MapToResponse(Payment payment)
    {
        return new PaymentResponse(
            payment.PublicId,
            payment.OrderPublicId,
            payment.UserId,
            payment.UserPublicId,
            payment.Amount,
            payment.Currency,
            payment.PaymentMethod,
            payment.PaymentStatus.ToString(),
            payment.PaymentReference,
            payment.SnapToken,
            payment.SnapRedirectUrl,
            payment.CreatedAt
        );
    }
}
