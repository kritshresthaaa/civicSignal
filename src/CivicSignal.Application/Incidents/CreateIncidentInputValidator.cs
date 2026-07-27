using FluentValidation;

namespace CivicSignal.Application.Incidents;

public sealed class CreateIncidentInputValidator : AbstractValidator<CreateIncidentInput>
{
    public CreateIncidentInputValidator()
    {
        RuleFor(input => input.Description)
            .NotEmpty()
            .MaximumLength(2_000);

        RuleFor(input => input.Latitude)
            .InclusiveBetween(-90, 90);

        RuleFor(input => input.Longitude)
            .InclusiveBetween(-180, 180);
    }
}
