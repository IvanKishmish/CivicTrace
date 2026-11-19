using CivicTrace.Application.Common;
using CivicTrace.Application.Features.Prozorro.Commands.ImportProcurement;
using CivicTrace.Application.Features.Prozorro.Commands.SyncPoltava;
using CivicTrace.WebApi.Contracts.Import;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CivicTrace.WebApi.Controllers;

[Authorize(Roles = Roles.Admin)]
[EnableRateLimiting("auth")]
public sealed class ImportController(IMediator mediator) : ApiController(mediator)
{
    [HttpPost("procurements")]
    public async Task<IActionResult> ImportProcurement(ImportProcurementRequest request, CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new ImportProcurementCommand(request.ApiId, request.DryRun, request.ReplaceExisting),
            ct);

        return HandleResult(result);
    }

    [HttpPost("sync")]
    public async Task<IActionResult> SyncPoltava([FromBody] SyncPoltavaRequest? request = null, CancellationToken ct = default)
    {
        var result = await Mediator.Send(
            new SyncPoltavaCommand(request?.MaxPages, request?.TargetNewCount),
            ct);

        return HandleResult(result);
    }
}

public sealed record SyncPoltavaRequest(int? MaxPages = null, int? TargetNewCount = null);