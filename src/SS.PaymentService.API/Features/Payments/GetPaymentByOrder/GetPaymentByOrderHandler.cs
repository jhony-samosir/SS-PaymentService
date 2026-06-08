using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SS.PaymentService.API.Features.Payments.Shared;
using SS.PaymentService.API.Infrastructure.Data;

namespace SS.PaymentService.API.Features.Payments.GetPaymentByOrder;

public class GetPaymentByOrderHandler(ApplicationDbContext dbContext) : IRequestHandler<GetPaymentByOrderQuery, IResult>
{
    public async Task<IResult> Handle(GetPaymentByOrderQuery request, CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(p => p.OrderPublicId == request.OrderPublicId, cancellationToken);

        if (payment == null)
        {
            return Results.NotFound(new { Message = $"Payment for Order (Id: {request.OrderPublicId}) not found." });
        }

        return Results.Ok(PaymentMapper.MapToResponse(payment));
    }
}
