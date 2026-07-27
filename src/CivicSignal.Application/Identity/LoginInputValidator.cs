using FluentValidation;

namespace CivicSignal.Application.Identity;

public sealed class LoginInputValidator : AbstractValidator<LoginInput>
{
    public LoginInputValidator()
    {
        RuleFor(input => input.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(input => input.Password)
            .NotEmpty();
    }
}
