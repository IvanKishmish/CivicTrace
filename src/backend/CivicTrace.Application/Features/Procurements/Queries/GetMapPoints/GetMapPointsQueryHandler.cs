using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Features.Procurements.Common;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Features.Procurements.Queries.GetMapPoints;

public sealed class GetMapPointsQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetMapPointsQuery, ErrorOr<IReadOnlyList<MapPointResponse>>>
{
    public async ValueTask<ErrorOr<IReadOnlyList<MapPointResponse>>> Handle(
        GetMapPointsQuery query,
        CancellationToken ct = default)
    {
        var rows = await dbContext.Procurements
            .AsNoTracking()
            .Where(p => p.Place.Latitude != null && p.Place.Longitude != null)
            .OrderByDescending(p => p.ExternalId)
            .Select(p => new
            {
                p.ExternalId,
                p.Title,
                p.Place.Address,
                p.Place.Type,
                Latitude = p.Place.Latitude!.Value,
                Longitude = p.Place.Longitude!.Value,
                p.Place.CoordinatesStatus
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new MapPointResponse(
                r.ExternalId,
                r.Title,
                r.Address,
                ProcurementMapper.PlaceTypeCode(r.Type),
                r.Latitude,
                r.Longitude,
                r.CoordinatesStatus is { } status ? ProcurementMapper.CoordinatesStatusCode(status) : null))
            .ToList();
    }
}