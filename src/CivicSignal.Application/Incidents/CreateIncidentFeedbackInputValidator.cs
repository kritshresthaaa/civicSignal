using FluentValidation;

namespace CivicSignal.Application.Incidents;

public sealed class CreateIncidentFeedbackInputValidator : AbstractValidator<CreateIncidentFeedbackInput>
{
    public CreateIncidentFeedbackInputValidator()
    {
        RuleFor(input => input.Rating)
            .InclusiveBetween(1, 5);

        RuleFor(input => input.Comment)
            .MaximumLength(2_000);
    }
}
