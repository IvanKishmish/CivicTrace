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
                p.Place.CoordinatesStatus,
                p.Status,
                BuyerName = p.Buyer.Name,
                ExpectedValue = p.ExpectedAmount != null ? p.ExpectedAmount.Value : (decimal?)null,
                ExpectedVat = p.ExpectedAmount != null ? p.ExpectedAmount.VatIncluded : null,
                ExpectedCurrency = p.ExpectedAmount != null ? p.ExpectedAmount.Currency : null,
                ContractValue = p.ContractAmount != null ? p.ContractAmount.Value : (decimal?)null,
                ContractVat = p.ContractAmount != null ? p.ContractAmount.VatIncluded : null,
                ContractCurrency = p.ContractAmount != null ? p.ContractAmount.Currency : null,
                CpvCode = p.Cpv.Code
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
                r.CoordinatesStatus is { } status ? ProcurementMapper.CoordinatesStatusCode(status) : null,
                ProcurementMapper.StatusCode(r.Status),
                ProcurementMapper.StatusLabel(r.Status),
                r.BuyerName,
                r.ExpectedValue,
                r.ContractValue,
                r.ContractCurrency ?? r.ExpectedCurrency ?? "UAH",
                r.ExpectedVat,
                r.ContractVat,
                r.CpvCode))
            .ToList();
    }
}
