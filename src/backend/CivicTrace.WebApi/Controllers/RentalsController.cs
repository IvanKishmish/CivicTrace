using CivicTrace.Application.Common;
using CivicTrace.Application.Features.Rentals.Commands.SyncRentals;
using CivicTrace.Application.Features.Rentals.Queries.ExportRentals;
using CivicTrace.Application.Features.Rentals.Queries.GetRentalAnalytics;
using CivicTrace.Application.Features.Rentals.Queries.GetRentalByOcid;
using CivicTrace.Application.Features.Rentals.Queries.GetRentalMap;
using CivicTrace.Application.Features.Rentals.Queries.GetRentals;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicTrace.WebApi.Controllers;

public sealed class RentalsController(IMediator mediator) : ApiController(mediator)
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetAll([FromQuery] GetRentalsQuery query, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(query, ct));

    [HttpGet("{ocid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetByOcid(string ocid, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new GetRentalByOcidQuery(ocid), ct));

    [HttpGet("map")]
    [AllowAnonymous]
    public async Task<IActionResult> GetMap(CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new GetRentalMapQuery(), ct));

    [HttpGet("map/geojson")]
    [AllowAnonymous]
    [Produces("application/geo+json")]
    public async Task<IActionResult> GetMapGeoJson(CancellationToken ct = default)
    {
        var result = await Mediator.Send(new GetRentalMapQuery(), ct);
        if (result.IsError) return HandleResult(result);

        return Ok(new
        {
            type = "FeatureCollection",
            features = result.Value.Points.Select(point => new
            {
                type = "Feature",
                id = point.Ocid,
                geometry = new
                {
                    type = "Point",
                    coordinates = new[] { point.Longitude, point.Latitude }
                },
                properties = new
                {
                    point.Ocid,
                    point.Title,
                    point.ObjectType,
                    point.Address,
                    point.District,
                    point.CoordinatesStatus,
                    point.AreaSqm,
                    point.StatusCode,
                    point.StatusLabel,
                    point.ListType,
                    point.CurrentContractValue,
                    point.RiskScore,
                    point.RiskFlags
                }
            })
        });
    }

    [HttpGet("analytics")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAnalytics(CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new GetRentalAnalyticsQuery(), ct));

    [HttpGet("export")]
    [AllowAnonymous]
    public async Task<IActionResult> Export([FromQuery] string format = "csv", CancellationToken ct = default)
    {
        var result = await Mediator.Send(new ExportRentalsQuery(format), ct);
        return result.IsError
            ? HandleResult(result)
            : File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPost("sync")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Sync([FromBody] SyncRentalsRequest? request = null, CancellationToken ct = default)
        => HandleResult(await Mediator.Send(new SyncRentalsCommand(request?.MaxBatchesPerSlice ?? 30), ct));
}

public sealed record SyncRentalsRequest(int? MaxBatchesPerSlice = null);
