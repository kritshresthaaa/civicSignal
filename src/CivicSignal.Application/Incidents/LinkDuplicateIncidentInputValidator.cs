using FluentValidation;

namespace CivicSignal.Application.Incidents;

public sealed class LinkDuplicateIncidentInputValidator : AbstractValidator<LinkDuplicateIncidentInput>
{
    public LinkDuplicateIncidentInputValidator()
    {
        RuleFor(input => input.DuplicateOfIncidentId)
            .NotEmpty();

        RuleFor(input => input.LinkedByUserId)
            .NotEmpty();

        RuleFor(input => input.Note)
            .MaximumLength(2_000);
    }
}
