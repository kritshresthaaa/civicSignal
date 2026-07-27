using FluentValidation;

namespace CivicSignal.Application.Incidents;

public sealed class AssignIncidentInputValidator : AbstractValidator<AssignIncidentInput>
{
    public AssignIncidentInputValidator()
    {
        RuleFor(input => input.AssignedTeam)
            .NotEmpty()
            .MaximumLength(160);

        RuleFor(input => input.AssignedAgencyCode)
            .MaximumLength(32);

        RuleFor(input => input.Note)
            .MaximumLength(2_000);

        RuleFor(input => input.AssignedByUserId)
            .NotEmpty();
    }
}
