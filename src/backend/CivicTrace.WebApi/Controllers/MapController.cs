using CivicTrace.Application.Features.Procurements.Queries.GetMapPoints;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicTrace.WebApi.Controllers;

public sealed class MapController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPoints(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetMapPointsQuery(), ct);
        return HandleResult(result);
    }
}