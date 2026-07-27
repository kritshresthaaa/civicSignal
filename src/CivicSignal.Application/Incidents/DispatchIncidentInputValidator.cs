using FluentValidation;

namespace CivicSignal.Application.Incidents;

public sealed class DispatchIncidentInputValidator : AbstractValidator<DispatchIncidentInput>
{
    public DispatchIncidentInputValidator()
    {
        RuleFor(input => input.DispatchedByUserId)
            .NotEmpty();

        RuleFor(input => input.Note)
            .MaximumLength(2_000);
    }
}
