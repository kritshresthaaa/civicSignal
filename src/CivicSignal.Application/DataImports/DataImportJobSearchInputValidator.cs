using FluentValidation;

namespace CivicSignal.Application.DataImports;

public sealed class DataImportJobSearchInputValidator : AbstractValidator<DataImportJobSearchInput>
{
    public DataImportJobSearchInputValidator()
    {
        RuleFor(input => input.Source)
            .MaximumLength(40);

        RuleFor(input => input.Status)
            .MaximumLength(40);

        RuleFor(input => input.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(input => input.PageSize)
            .InclusiveBetween(1, 200);
    }
}
