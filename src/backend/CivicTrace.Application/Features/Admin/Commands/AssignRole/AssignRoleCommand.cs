using ErrorOr;
using Mediator;

namespace CivicTrace.Application.Features.Admin.Commands.AssignRole;

public sealed record AssignRoleCommand(Guid UserId, string Role) : ICommand<ErrorOr<Updated>>;