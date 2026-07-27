using FluentValidation;

namespace CivicSignal.Application.Identity;

public sealed class RegisterUserInputValidator : AbstractValidator<RegisterUserInput>
{
    public RegisterUserInputValidator()
    {
        RuleFor(input => input.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(input => input.Password)
            .NotEmpty()
            .MinimumLength(10)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");

        RuleFor(input => input.DisplayName)
            .MaximumLength(160);
    }
}
