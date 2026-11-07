using FluentValidation;

namespace CivicTrace.Application.Features.Prozorro.Commands.ImportProcurement;

public sealed class ImportProcurementCommandValidator : AbstractValidator<ImportProcurementCommand>
{
    public ImportProcurementCommandValidator()
    {
        RuleFor(x => x.ApiId)
            .NotEmpty().WithMessage("ApiId is required")
            .Matches("^[0-9a-fA-F]{32}$").WithMessage("ApiId must be the 32-character internal tender id (hex)");
    }
}