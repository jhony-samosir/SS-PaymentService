namespace SS.PaymentService.API.Domain.Enums;

public enum PaymentStatus
{
    Pending,
    WaitingPayment,
    Paid,
    Failed,
    Expired,
    Cancelled,
    Refunded
}
