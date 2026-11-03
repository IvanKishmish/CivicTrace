using CivicTrace.Application.Common;
using CivicTrace.Application.Features.Procurements.Common;
using FluentValidation;

namespace CivicTrace.Application.Features.Procurements.Queries.GetProcurements;

public sealed class GetProcurementsQueryValidator : PagedQueryValidator<GetProcurementsQuery>
{
    public GetProcurementsQueryValidator()
    {
        RuleFor(x => x.Search)
            .MaximumLength(100).WithMessage("Search cannot be longer than 100 characters.");

        RuleFor(x => x.Status)
            .Must(s => ProcurementMapper.StatusCodes.Contains(s))
            .When(x => x.Status is not null)
            .WithMessage($"Status must be one of: {string.Join(", ", ProcurementMapper.StatusCodes)}.");

        RuleFor(x => x.Kind)
            .Must(k => ProcurementMapper.KindCodes.Contains(k))
            .When(x => x.Kind is not null)
            .WithMessage($"Kind must be one of: {string.Join(", ", ProcurementMapper.KindCodes)}.");

        RuleFor(x => x.Method)
            .Must(m => ProcurementMapper.MethodCodes.Contains(m))
            .When(x => x.Method is not null)
            .WithMessage($"Method must be one of: {string.Join(", ", ProcurementMapper.MethodCodes)}.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100)
            .When(x => x.Year is not null)
            .WithMessage("Year must be between 2000 and 2100.");

        RuleFor(x => x.BuyerEdrpou)
            .Matches("^[0-9]{8}$")
            .When(x => !string.IsNullOrEmpty(x.BuyerEdrpou))
            .WithMessage("BuyerEdrpou must consist of exactly 8 digits.");
    }
}