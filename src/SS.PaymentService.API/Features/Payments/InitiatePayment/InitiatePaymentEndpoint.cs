using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace SS.PaymentService.API.Features.Payments.InitiatePayment;

public static class InitiatePaymentEndpoint
{
    public static IEndpointRouteBuilder MapInitiatePaymentEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/payments", async (InitiatePaymentRequest request, ISender sender) =>
        {
            var command = new InitiatePaymentCommand(
                request.OrderPublicId,
                request.UserId,
                request.UserPublicId,
                request.Amount,
                request.Currency,
                request.PaymentMethod
            );

            return await sender.Send(command);
        })
        .WithName("InitiatePayment")
        .WithOpenApi();

        return app;
    }
}
