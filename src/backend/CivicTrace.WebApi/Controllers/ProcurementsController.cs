using CivicTrace.Application.Common;
using CivicTrace.Application.Features.Procurements.Commands.MarkProcurementVerified;
using CivicTrace.Application.Features.Procurements.Queries.GetProcurementByExternalId;
using CivicTrace.Application.Features.Procurements.Queries.GetProcurements;
using CivicTrace.WebApi.Contracts.Procurements;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicTrace.WebApi.Controllers;

public sealed class ProcurementsController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] GetProcurementsQuery query, CancellationToken ct = default)
    {
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }

    [HttpGet("{externalId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByExternalId(string externalId, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetProcurementByExternalIdQuery(externalId), ct);
        return HandleResult(result);
    }

    [HttpPost("{externalId}/verify")]
    [Authorize(Roles = $"{Roles.Editor},{Roles.Admin}")]
    public async Task<IActionResult> Verify(string externalId, VerifyProcurementRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(new MarkProcurementVerifiedCommand(externalId, request.VerifiedAt), ct);
        return HandleResult(result);
    }
}