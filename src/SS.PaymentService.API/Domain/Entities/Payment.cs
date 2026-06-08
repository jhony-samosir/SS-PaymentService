using System;
using System.Collections.Generic;
using SS.PaymentService.API.Domain.Common;
using SS.PaymentService.API.Domain.Enums;

namespace SS.PaymentService.API.Domain.Entities;

public class Payment : BaseEntity
{
    public Guid OrderPublicId { get; set; }
    public int UserId { get; set; }
    public Guid UserPublicId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "IDR";
    public string PaymentMethod { get; set; } = "Midtrans";
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public string? PaymentReference { get; set; }
    public string? SnapToken { get; set; }
    public string? SnapRedirectUrl { get; set; }

    public ICollection<PaymentRefund> Refunds { get; set; } = new List<PaymentRefund>();
}
