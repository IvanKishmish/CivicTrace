using FluentValidation;

namespace CivicTrace.Application.Features.Export.Queries.ExportProcurements;

public sealed class ExportProcurementsQueryValidator : AbstractValidator<ExportProcurementsQuery>
{
    public ExportProcurementsQueryValidator()
    {
        RuleFor(x => x.Format)
            .Must(format => format is "csv" or "json")
            .WithMessage("Format must be 'csv' or 'json'.");
    }
}