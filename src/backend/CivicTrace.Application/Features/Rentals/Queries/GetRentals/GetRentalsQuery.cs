using CivicTrace.Application.Common;
using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Features.Rentals.Common;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Features.Rentals.Queries.GetRentals;

public sealed record GetRentalsQuery(
    string? Search = null,
    string? Status = null,
    string? ListType = null,
    string? ObjectType = null,
    string? District = null,
    int? Year = null,
    decimal? MinArea = null,
    decimal? MaxArea = null,
    bool OnlyUnenriched = false,
    bool OnlyWithDefects = false,
    string SortBy = "year_desc",
    int PageNumber = 1,
    int PageSize = 15) : IQuery<ErrorOr<PagedResponse<RentalObjectResponse>>>;

public sealed class GetRentalsQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetRentalsQuery, ErrorOr<PagedResponse<RentalObjectResponse>>>
{
    public async ValueTask<ErrorOr<PagedResponse<RentalObjectResponse>>> Handle(
        GetRentalsQuery request, CancellationToken ct = default)
    {
        var page = Math.Max(1, request.PageNumber);
        var size = Math.Clamp(request.PageSize, 1, 100);

        var q = dbContext.RentalObjects.AsNoTracking().Include(r => r.Auctions).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var pattern = $"%{request.Search.Trim()}%";
            q = q.Where(r =>
                EF.Functions.ILike(r.Ocid, pattern) ||
                EF.Functions.ILike(r.NormalizedTitle, pattern) ||
                (r.Address != null && EF.Functions.ILike(r.Address, pattern)) ||
                (r.PropertyOwnerName != null && EF.Functions.ILike(r.PropertyOwnerName, pattern)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status)) q = q.Where(r => r.StatusCode == request.Status);
        if (!string.IsNullOrWhiteSpace(request.ListType)) q = q.Where(r => r.ListType == request.ListType);
        if (!string.IsNullOrWhiteSpace(request.ObjectType)) q = q.Where(r => r.ObjectType == request.ObjectType);
        if (!string.IsNullOrWhiteSpace(request.District)) q = q.Where(r => r.District == request.District);
        if (request.Year is { } year) q = q.Where(r => r.CreatedYear == year);
        if (request.MinArea is { } minA) q = q.Where(r => r.AreaSqm != null && r.AreaSqm >= minA);
        if (request.MaxArea is { } maxA) q = q.Where(r => r.AreaSqm != null && r.AreaSqm <= maxA);
        if (request.OnlyUnenriched) q = q.Where(r => r.EnrichmentStatus != "Enriched");
        if (request.OnlyWithDefects) q = q.Where(r => r.DataDefectsNotes != null);

        q = request.SortBy switch
        {
            "area_desc" => q.OrderByDescending(r => r.AreaSqm ?? -1).ThenByDescending(r => r.Ocid),
            "area_asc" => q.OrderBy(r => r.AreaSqm ?? decimal.MaxValue).ThenByDescending(r => r.Ocid),
            "ocid_asc" => q.OrderBy(r => r.Ocid),
            _ => q.OrderByDescending(r => r.EnrichmentStatus == "Enriched")
                  .ThenByDescending(r => r.CreatedYear ?? 0)
                  .ThenByDescending(r => r.Ocid)
        };

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var allAuctions = await dbContext.RentalAuctions.AsNoTracking().ToListAsync(ct);
        var medianRate = RentalMapper.ComputeMedianRate(allAuctions);
        var cfg = RiskRulesConfig.Load();

        return new PagedResponse<RentalObjectResponse>(
            items.Select(r => RentalMapper.ToResponse(r, medianRate, cfg)).ToList(), page, size, total);
    }
}