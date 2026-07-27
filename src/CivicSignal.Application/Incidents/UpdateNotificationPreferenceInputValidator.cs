using FluentValidation;

namespace CivicSignal.Application.Incidents;

public sealed class UpdateNotificationPreferenceInputValidator : AbstractValidator<UpdateNotificationPreferenceInput>
{
    public UpdateNotificationPreferenceInputValidator()
    {
        RuleFor(input => input.Channel)
            .NotEmpty()
            .When(input => input.AlertsEnabled)
            .WithMessage("Notification channel is required when alerts are enabled.");

        RuleFor(input => input.Channel)
            .MaximumLength(80);
    }
}
