using System;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SS.PaymentService.API.Features.Refunds.InitiateRefund;

public static class InitiateRefundEndpoint
{
    public static IEndpointRouteBuilder MapInitiateRefundEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/payments/{paymentPublicId:guid}/refund", async (Guid paymentPublicId, InitiateRefundRequest request, ISender sender) =>
        {
            var command = new InitiateRefundCommand(paymentPublicId, request.RefundAmount, request.RefundReason);
            return await sender.Send(command);
        })
        .WithName("InitiateRefund")
        .WithOpenApi();

        return app;
    }
}
