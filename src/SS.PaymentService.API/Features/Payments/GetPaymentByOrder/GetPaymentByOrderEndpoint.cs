using System;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SS.PaymentService.API.Features.Payments.GetPaymentByOrder;

public static class GetPaymentByOrderEndpoint
{
    public static IEndpointRouteBuilder MapGetPaymentByOrderEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/payments/order/{orderPublicId:guid}", async (Guid orderPublicId, ISender sender) =>
        {
            var query = new GetPaymentByOrderQuery(orderPublicId);
            return await sender.Send(query);
        })
        .WithName("GetPaymentByOrder")
        .WithOpenApi();

        return app;
    }
}
