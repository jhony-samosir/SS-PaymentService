using System;
using MediatR;
using Microsoft.AspNetCore.Http;

namespace SS.PaymentService.API.Features.Payments.CancelPayment;

public record CancelPaymentCommand(Guid PaymentPublicId) : IRequest<IResult>;
