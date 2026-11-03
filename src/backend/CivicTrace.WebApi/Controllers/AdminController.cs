using CivicTrace.Application.Common;
using CivicTrace.Application.Features.Admin.Commands.AssignRole;
using CivicTrace.WebApi.Contracts.Admin;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicTrace.WebApi.Controllers;

[Authorize(Roles = Roles.Admin)]
public sealed class AdminController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("users/{userId:guid}/roles")]
    public async Task<IActionResult> AssignRole(Guid userId, AssignRoleRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new AssignRoleCommand(userId, request.Role), ct);
        return HandleResult(result);
    }
}