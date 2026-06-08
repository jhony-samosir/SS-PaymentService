using FluentValidation;

namespace SS.PaymentService.API.Features.Refunds.InitiateRefund;

public class InitiateRefundValidator : AbstractValidator<InitiateRefundCommand>
{
    public InitiateRefundValidator()
    {
        RuleFor(x => x.PaymentPublicId).NotEmpty();
        RuleFor(x => x.RefundAmount).GreaterThan(0);
        RuleFor(x => x.RefundReason).NotEmpty().MaximumLength(500);
    }
}
