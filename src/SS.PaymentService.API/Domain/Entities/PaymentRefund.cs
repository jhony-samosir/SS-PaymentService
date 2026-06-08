using System;
using SS.PaymentService.API.Domain.Common;
using SS.PaymentService.API.Domain.Enums;

namespace SS.PaymentService.API.Domain.Entities;

public class PaymentRefund : BaseEntity
{
    public int PaymentId { get; set; }
    public Payment Payment { get; set; } = null!;
    public decimal RefundAmount { get; set; }
    public string? RefundReason { get; set; }
    public RefundStatus RefundStatus { get; set; } = RefundStatus.Pending;
    public string? RefundReference { get; set; }
}
