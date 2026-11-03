using CivicTrace.Application.Common.Interfaces;
using ErrorOr;
using FluentValidation;
using Mediator;

namespace CivicTrace.Application.Features.Admin.Commands.AssignRole;

public sealed class AssignRoleCommandHandler(
    IIdentityService identityService,
    IValidator<AssignRoleCommand> validator)
    : ICommandHandler<AssignRoleCommand, ErrorOr<Updated>>
{
    public async ValueTask<ErrorOr<Updated>> Handle(AssignRoleCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        // Доступ лише для Admin обмежується в WebApi атрибутом [Authorize(Roles = Roles.Admin)]
        return await identityService.AssignRoleAsync(command.UserId, command.Role, ct);
    }
}