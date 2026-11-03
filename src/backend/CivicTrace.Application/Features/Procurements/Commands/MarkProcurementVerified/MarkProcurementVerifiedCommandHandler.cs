using CivicTrace.Application.Common.Interfaces;
using ErrorOr;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Features.Procurements.Commands.MarkProcurementVerified;

public sealed class MarkProcurementVerifiedCommandHandler(
    IAppDbContext dbContext,
    ICurrentUserService currentUser,
    IValidator<MarkProcurementVerifiedCommand> validator)
    : ICommandHandler<MarkProcurementVerifiedCommand, ErrorOr<Updated>>
{
    public async ValueTask<ErrorOr<Updated>> Handle(MarkProcurementVerifiedCommand command, CancellationToken ct = default)
    {
        if (currentUser.UserId is null)
            return Error.Unauthorized("Auth.NotAuthenticated", "User is not authenticated.");

        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var userId = currentUser.UserId.Value;

        if (!await dbContext.UserProfiles.AnyAsync(u => u.Id == userId, ct))
            return Error.Forbidden("Auth.ProfileMissing", "The current user has no profile.");

        var procurement = await dbContext.Procurements
            .FirstOrDefaultAsync(p => p.ExternalId == command.ExternalId, ct);

        if (procurement is null)
            return Error.NotFound("Procurement.NotFound", "Procurement not found.");

        var verifiedAt = command.VerifiedAt ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var result = procurement.MarkVerified(verifiedAt, userId);

        if (result.IsError)
            return result.Errors;

        await dbContext.SaveChangesAsync(ct);

        return Result.Updated;
    }
}