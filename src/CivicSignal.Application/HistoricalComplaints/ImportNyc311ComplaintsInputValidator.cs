using FluentValidation;

namespace CivicSignal.Application.HistoricalComplaints;

public sealed class ImportNyc311ComplaintsInputValidator : AbstractValidator<ImportNyc311ComplaintsInput>
{
    public ImportNyc311ComplaintsInputValidator()
    {
        RuleFor(input => input.Limit)
            .InclusiveBetween(1, 5_000)
            .When(input => input.Limit.HasValue);

        RuleFor(input => input.DaysBack)
            .InclusiveBetween(1, 3_650)
            .When(input => input.DaysBack.HasValue);

        RuleFor(input => input.ComplaintType)
            .MaximumLength(200);

        RuleFor(input => input.Borough)
            .MaximumLength(80);
    }
}
