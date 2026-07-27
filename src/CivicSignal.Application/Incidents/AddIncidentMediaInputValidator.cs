using FluentValidation;

namespace CivicSignal.Application.Incidents;

public sealed class AddIncidentMediaInputValidator : AbstractValidator<AddIncidentMediaInput>
{
    public AddIncidentMediaInputValidator()
    {
        RuleFor(input => input.FileName)
            .NotEmpty()
            .MaximumLength(260);

        RuleFor(input => input.ContentType)
            .NotEmpty()
            .MaximumLength(160);

        RuleFor(input => input.StorageUri)
            .NotEmpty()
            .MaximumLength(2_048);
    }
}
