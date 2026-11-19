using CivicTrace.Application.Common.Interfaces;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Features.Rentals.Queries.GetRentalMap;

public sealed record RentalMapPointDto(
    string Ocid,
    string Title,
    string ObjectType,
    string? Address,
    string? District,
    double Latitude,
    double Longitude,
    string? CoordinatesStatus,
    decimal? AreaSqm,
    string StatusCode,
    string StatusLabel,
    string ListType,
    decimal? CurrentContractValue);

public sealed record RentalMapResponse(
    int TotalObjects,
    int EnrichedCount,
    int MappedCount,
    int UnmappedWithAddressCount,
    int UnmappedWithoutAddressCount,
    double UngeocodedPercent,
    IReadOnlyList<RentalMapPointDto> Points);

public sealed record GetRentalMapQuery : IQuery<ErrorOr<RentalMapResponse>>;

public sealed class GetRentalMapQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetRentalMapQuery, ErrorOr<RentalMapResponse>>
{
    public async ValueTask<ErrorOr<RentalMapResponse>> Handle(GetRentalMapQuery query, CancellationToken ct = default)
    {
        var all = await dbContext.RentalObjects
            .AsNoTracking()
            .Select(r => new
            {
                r.Ocid,
                r.NormalizedTitle,
                r.ObjectType,
                r.Address,
                r.District,
                r.Latitude,
                r.Longitude,
                r.CoordinatesStatus,
                r.AreaSqm,
                r.StatusCode,
                r.StatusLabel,
                r.ListType,
                r.CurrentContractValue,
                r.EnrichmentStatus
            })
            .ToListAsync(ct);

        var points = all
            .Where(r => r.Latitude != null && r.Longitude != null)
            .OrderByDescending(r => r.Ocid)
            .Select(r => new RentalMapPointDto(
                r.Ocid,
                r.NormalizedTitle,
                r.ObjectType,
                r.Address,
                r.District,
                r.Latitude!.Value,
                r.Longitude!.Value,
                r.CoordinatesStatus,
                r.AreaSqm,
                r.StatusCode,
                r.StatusLabel,
                r.ListType,
                r.CurrentContractValue))
            .ToList();

        var total = all.Count;
        var mapped = points.Count;
        var unmappedWithAddr = all.Count(r => r.Latitude == null && !string.IsNullOrWhiteSpace(r.Address));
        var unmappedNoAddr = total - mapped - unmappedWithAddr;
        var pct = total == 0 ? 0 : Math.Round((total - mapped) * 100.0 / total, 1);

        return new RentalMapResponse(
            total,
            all.Count(r => r.EnrichmentStatus == "Enriched"),
            mapped,
            unmappedWithAddr,
            unmappedNoAddr,
            pct,
            points);
    }
}