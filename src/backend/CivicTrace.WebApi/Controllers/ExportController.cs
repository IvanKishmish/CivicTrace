using CivicTrace.Application.Features.Export.Queries.ExportProcurements;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicTrace.WebApi.Controllers;

public sealed class ExportController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet("procurements")]
    [AllowAnonymous]
    public async Task<IActionResult> ExportProcurements([FromQuery] ExportProcurementsQuery query, CancellationToken ct = default)
    {
        var result = await Mediator.Send(query, ct);

        return result.IsError
            ? HandleResult(result)
            : File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }
}