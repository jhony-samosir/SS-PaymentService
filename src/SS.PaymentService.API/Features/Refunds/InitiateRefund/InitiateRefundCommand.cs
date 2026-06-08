using System;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace SS.PaymentService.API.Features.Refunds.InitiateRefund;

public record InitiateRefundCommand(
    Guid PaymentPublicId,
    decimal RefundAmount,
    string RefundReason
) : IRequest<IResult>;
