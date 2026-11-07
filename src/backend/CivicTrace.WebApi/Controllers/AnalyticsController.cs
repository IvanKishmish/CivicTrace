using CivicTrace.Application.Features.Analytics.Queries.GetAnalytics;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicTrace.WebApi.Controllers;

public sealed class AnalyticsController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get([FromQuery] GetAnalyticsQuery query, CancellationToken ct = default)
    {
        var result = await Mediator.Send(query, ct);
        return HandleResult(result);
    }
}