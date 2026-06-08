using System;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace SS.PaymentService.API.Features.Payments.GetPaymentByOrder;

public record GetPaymentByOrderQuery(Guid OrderPublicId) : IRequest<IResult>;
