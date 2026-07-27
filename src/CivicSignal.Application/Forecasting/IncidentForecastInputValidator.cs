using FluentValidation;

namespace CivicSignal.Application.Forecasting;

public sealed class IncidentForecastInputValidator : AbstractValidator<IncidentForecastInput>
{
    public IncidentForecastInputValidator()
    {
        RuleFor(input => input.HistoryDays)
            .InclusiveBetween(7, 365);

        RuleFor(input => input.HorizonDays)
            .InclusiveBetween(1, 30);

        RuleFor(input => input.Category)
            .MaximumLength(80);

        RuleFor(input => input.AgencyCode)
            .MaximumLength(32);
    }
}
