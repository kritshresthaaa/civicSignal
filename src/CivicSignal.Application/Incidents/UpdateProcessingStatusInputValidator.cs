using CivicSignal.Domain.Incidents;
using FluentValidation;

namespace CivicSignal.Application.Incidents;

public sealed class UpdateProcessingStatusInputValidator : AbstractValidator<UpdateProcessingStatusInput>
{
    public UpdateProcessingStatusInputValidator()
    {
        RuleFor(input => input.StepName)
            .NotEmpty()
            .MaximumLength(160);

        RuleFor(input => input.Status)
            .NotEmpty()
            .Must(BeSupportedStatus)
            .WithMessage("Processing status must be InProgress, Succeeded, or Failed.");

        RuleFor(input => input.ErrorMessage)
            .MaximumLength(2_000);

        RuleFor(input => input.ErrorMessage)
            .NotEmpty()
            .When(input => IsFailedStatus(input.Status))
            .WithMessage("An error message is required for failed processing steps.");
    }

    private static bool BeSupportedStatus(string status)
    {
        return Enum.TryParse<ProcessingStepStatus>(status, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(ProcessingStepStatus), parsed);
    }

    private static bool IsFailedStatus(string status)
    {
        return Enum.TryParse<ProcessingStepStatus>(status, ignoreCase: true, out var parsed)
            && parsed is ProcessingStepStatus.Failed;
    }
}
