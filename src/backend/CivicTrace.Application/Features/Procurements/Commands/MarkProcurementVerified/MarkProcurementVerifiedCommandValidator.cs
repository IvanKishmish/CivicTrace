using FluentValidation;

namespace CivicTrace.Application.Features.Procurements.Commands.MarkProcurementVerified;

public sealed class MarkProcurementVerifiedCommandValidator : AbstractValidator<MarkProcurementVerifiedCommand>
{
    public MarkProcurementVerifiedCommandValidator()
    {
        RuleFor(x => x.ExternalId)
            .NotEmpty().WithMessage("Procurement id is required");

        RuleFor(x => x.VerifiedAt)
            .Must(date => date is null || date <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Verification date cannot be in the future");
    }
}