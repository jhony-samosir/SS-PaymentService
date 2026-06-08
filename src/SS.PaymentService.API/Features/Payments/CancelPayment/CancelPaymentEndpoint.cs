using System;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SS.PaymentService.API.Features.Payments.CancelPayment;

public static class CancelPaymentEndpoint
{
    public static IEndpointRouteBuilder MapCancelPaymentEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/payments/{paymentPublicId:guid}/cancel", async (Guid paymentPublicId, ISender sender) =>
        {
            var command = new CancelPaymentCommand(paymentPublicId);
            return await sender.Send(command);
        })
        .WithName("CancelPayment")
        .WithOpenApi();

        return app;
    }
}
