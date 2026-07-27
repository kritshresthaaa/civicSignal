using CivicSignal.Domain.Incidents;
using FluentValidation;

namespace CivicSignal.Application.Incidents;

public sealed class ReviewIncidentInputValidator : AbstractValidator<ReviewIncidentInput>
{
    public ReviewIncidentInputValidator()
    {
        RuleFor(input => input.Decision)
            .NotEmpty()
            .Must(BeSupportedDecision)
            .WithMessage("Review decision must be Approved, Rejected, or NeedsMoreInfo.");

        RuleFor(input => input.Note)
            .MaximumLength(2_000);

        RuleFor(input => input.Note)
            .NotEmpty()
            .When(input => RequiresNote(input.Decision))
            .WithMessage("A review note is required for rejected or needs-more-info decisions.");

        RuleFor(input => input.CorrectedCategory)
            .MaximumLength(80);

        RuleFor(input => input.CorrectedAgencyCode)
            .MaximumLength(32);

        RuleFor(input => input.CorrectedSeverity)
            .Must(BeSupportedSeverity)
            .When(input => !string.IsNullOrWhiteSpace(input.CorrectedSeverity))
            .WithMessage("Corrected severity must be Low, Medium, High, or Critical.");

        RuleFor(input => input.DuplicateOfIncidentId)
            .Must(duplicateId => duplicateId is null || duplicateId != Guid.Empty)
            .WithMessage("Duplicate incident id cannot be empty.");

        RuleFor(input => input)
            .Must(HaveCorrectionOrNoteWhenPredictionRejected)
            .WithMessage("A rejected AI prediction needs a correction or reviewer note.");

        RuleFor(input => input.ReviewerUserId)
            .NotEmpty();
    }

    private static bool BeSupportedDecision(string decision)
    {
        return Enum.TryParse<ReviewDecision>(decision, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(ReviewDecision), parsed);
    }

    private static bool RequiresNote(string decision)
    {
        return Enum.TryParse<ReviewDecision>(decision, ignoreCase: true, out var parsed)
            && parsed is ReviewDecision.Rejected or ReviewDecision.NeedsMoreInfo;
    }

    private static bool BeSupportedSeverity(string? severity)
    {
        return Enum.TryParse<IncidentSeverity>(severity, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(IncidentSeverity), parsed);
    }

    private static bool HaveCorrectionOrNoteWhenPredictionRejected(ReviewIncidentInput input)
    {
        if (input.AcceptedPrediction is not false)
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(input.Note)
            || !string.IsNullOrWhiteSpace(input.CorrectedCategory)
            || !string.IsNullOrWhiteSpace(input.CorrectedAgencyCode)
            || !string.IsNullOrWhiteSpace(input.CorrectedSeverity)
            || input.DuplicateOfIncidentId is not null;
    }
}
