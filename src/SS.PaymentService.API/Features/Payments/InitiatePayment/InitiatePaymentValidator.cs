using FluentValidation;

namespace SS.PaymentService.API.Features.Payments.InitiatePayment;

public class InitiatePaymentValidator : AbstractValidator<InitiatePaymentCommand>
{
    public InitiatePaymentValidator()
    {
        RuleFor(x => x.OrderPublicId).NotEmpty();
        RuleFor(x => x.UserId).GreaterThan(0);
        RuleFor(x => x.UserPublicId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().MaximumLength(10);
        RuleFor(x => x.PaymentMethod).NotEmpty().MaximumLength(50);
    }
}
