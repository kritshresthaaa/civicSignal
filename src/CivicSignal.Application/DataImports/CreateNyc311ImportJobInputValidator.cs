using FluentValidation;

namespace CivicSignal.Application.DataImports;

public sealed class CreateNyc311ImportJobInputValidator : AbstractValidator<CreateNyc311ImportJobInput>
{
    public CreateNyc311ImportJobInputValidator()
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
