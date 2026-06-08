namespace SS.PaymentService.API.Features.Refunds.InitiateRefund;

public record InitiateRefundRequest(
    decimal RefundAmount,
    string RefundReason
);
