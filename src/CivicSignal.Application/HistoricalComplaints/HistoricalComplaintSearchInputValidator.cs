using FluentValidation;

namespace CivicSignal.Application.HistoricalComplaints;

public sealed class HistoricalComplaintSearchInputValidator : AbstractValidator<HistoricalComplaintSearchInput>
{
    public HistoricalComplaintSearchInputValidator()
    {
        RuleFor(input => input.Query)
            .MaximumLength(200);

        RuleFor(input => input.Category)
            .MaximumLength(80);

        RuleFor(input => input.ComplaintType)
            .MaximumLength(200);

        RuleFor(input => input.Agency)
            .MaximumLength(40);

        RuleFor(input => input.Status)
            .MaximumLength(80);

        RuleFor(input => input.Borough)
            .MaximumLength(80);

        RuleFor(input => input.Latitude)
            .InclusiveBetween(-90, 90)
            .When(input => input.Latitude.HasValue);

        RuleFor(input => input.Longitude)
            .InclusiveBetween(-180, 180)
            .When(input => input.Longitude.HasValue);

        RuleFor(input => input.RadiusMeters)
            .InclusiveBetween(1, 25_000)
            .When(input => input.RadiusMeters.HasValue);

        RuleFor(input => input.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(input => input.PageSize)
            .InclusiveBetween(1, 500);

        RuleFor(input => input)
            .Must(HaveBothCoordinatesOrNeither)
            .WithMessage("Latitude and longitude must be supplied together.");

        RuleFor(input => input)
            .Must(HaveValidDateRange)
            .WithMessage("CreatedFrom cannot be after CreatedTo.");
    }

    private static bool HaveBothCoordinatesOrNeither(HistoricalComplaintSearchInput input)
    {
        return input.Latitude.HasValue == input.Longitude.HasValue;
    }

    private static bool HaveValidDateRange(HistoricalComplaintSearchInput input)
    {
        return input.CreatedFrom is null
            || input.CreatedTo is null
            || input.CreatedFrom <= input.CreatedTo;
    }
}
