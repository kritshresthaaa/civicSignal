using FluentValidation;

namespace CivicSignal.Application.Incidents;

public sealed class CreateIncidentUpdateRequestInputValidator : AbstractValidator<CreateIncidentUpdateRequestInput>
{
    public CreateIncidentUpdateRequestInputValidator()
    {
        RuleFor(input => input.Message)
            .NotEmpty()
            .MaximumLength(2_000);
    }
}
