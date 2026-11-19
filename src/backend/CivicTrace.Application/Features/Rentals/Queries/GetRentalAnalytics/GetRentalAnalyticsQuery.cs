using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Features.Rentals.Common;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Features.Rentals.Queries.GetRentalAnalytics;

public sealed record RentalBucketDto(string Key, string Label, int Count, decimal TotalAreaSqm);

public sealed record RentalAnalyticsResponse(
    int TotalObjects,
    int EnrichedCount,
    int NotEnrichedCount,
    int MappedCount,
    int AuctionsCount,
    int ObjectsWithoutAuctionsCount,
    decimal TotalAreaSqm,
    decimal FreeAreaSqm,
    decimal? MedianRatePerSqmMonth,
    decimal? EstimatedLostAnnualIncomeUah,
    DateTimeOffset? LastUpdatedAt,
    IReadOnlyList<RentalBucketDto> ByStatus,
    IReadOnlyList<RentalBucketDto> ByObjectType,
    IReadOnlyList<RentalBucketDto> ByYear,
    IReadOnlyList<RentalBucketDto> ByOwner,
    IReadOnlyList<RentalBucketDto> ByRiskFlag,
    IReadOnlyList<RentalObjectResponse> TopRiskObjects,
    IReadOnlyList<RentalObjectResponse> UnenrichedSample,
    IReadOnlyList<RiskRuleDefinitionDto> RiskRules);

public sealed record GetRentalAnalyticsQuery : IQuery<ErrorOr<RentalAnalyticsResponse>>;

public sealed class GetRentalAnalyticsQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<GetRentalAnalyticsQuery, ErrorOr<RentalAnalyticsResponse>>
{
    public async ValueTask<ErrorOr<RentalAnalyticsResponse>> Handle(GetRentalAnalyticsQuery query, CancellationToken ct = default)
    {
        var objects = await dbContext.RentalObjects.AsNoTracking().Include(r => r.Auctions).ToListAsync(ct);
        var auctions = await dbContext.RentalAuctions.AsNoTracking().ToListAsync(ct);

        var cfg = RiskRulesConfig.Load();
        var medianRate = RentalMapper.ComputeMedianRate(auctions);
        var responses = objects.Select(o => RentalMapper.ToResponse(o, medianRate, cfg)).ToList();

        var freeArea = objects.Where(o => o.StatusCode == "free").Sum(o => o.AreaSqm ?? 0m);
        decimal? lostAnnual = medianRate is > 0 && freeArea > 0 ? Math.Round(freeArea * medianRate.Value * 12m, 2) : null;

        var byStatus = objects.GroupBy(o => (o.StatusCode, o.StatusLabel))
            .Select(g => new RentalBucketDto(g.Key.StatusCode, g.Key.StatusLabel, g.Count(), g.Sum(x => x.AreaSqm ?? 0m)))
            .OrderByDescending(x => x.Count).ToList();

        var byType = objects.GroupBy(o => o.ObjectType)
            .Select(g => new RentalBucketDto(g.Key, g.Key, g.Count(), g.Sum(x => x.AreaSqm ?? 0m)))
            .OrderByDescending(x => x.Count).ToList();

        var byYear = objects.GroupBy(o => o.CreatedYear?.ToString() ?? "Невідомо")
            .Select(g => new RentalBucketDto(g.Key, g.Key, g.Count(), g.Sum(x => x.AreaSqm ?? 0m)))
            .OrderByDescending(x => x.Key).ToList();

        var byOwner = objects.Where(o => !string.IsNullOrWhiteSpace(o.PropertyOwnerName))
            .GroupBy(o => o.PropertyOwnerName!)
            .Select(g => new RentalBucketDto(g.Key, g.Key, g.Count(), g.Sum(x => x.AreaSqm ?? 0m)))
            .OrderByDescending(x => x.Count).Take(12).ToList();

        var ruleTitleMap = RiskEvaluator.DescribeRules(cfg).ToDictionary(r => r.Code, r => r.Title);
        var byRiskFlag = responses
            .SelectMany(r => r.RiskFlags.Select(f => new { r.AreaSqm, f.Code }))
            .GroupBy(x => x.Code)
            .Select(g => new RentalBucketDto(g.Key, ruleTitleMap.GetValueOrDefault(g.Key, g.Key), g.Count(), g.Sum(x => x.AreaSqm ?? 0m)))
            .OrderByDescending(x => x.Count).ToList();

        return new RentalAnalyticsResponse(
            TotalObjects: objects.Count,
            EnrichedCount: objects.Count(o => o.EnrichmentStatus == "Enriched"),
            NotEnrichedCount: objects.Count(o => o.EnrichmentStatus != "Enriched"),
            MappedCount: objects.Count(o => o.Latitude != null && o.Longitude != null),
            AuctionsCount: auctions.Count,
            ObjectsWithoutAuctionsCount: objects.Count(o => o.Auctions.Count == 0),
            TotalAreaSqm: objects.Sum(o => o.AreaSqm ?? 0m),
            FreeAreaSqm: freeArea,
            MedianRatePerSqmMonth: medianRate,
            EstimatedLostAnnualIncomeUah: lostAnnual,
            LastUpdatedAt: objects.Where(o => o.LastSyncedAt != null).Max(o => o.LastSyncedAt),
            ByStatus: byStatus,
            ByObjectType: byType,
            ByYear: byYear,
            ByOwner: byOwner,
            ByRiskFlag: byRiskFlag,
            TopRiskObjects: responses.Where(r => r.RiskScore > 0).OrderByDescending(r => r.RiskScore).ThenByDescending(r => r.AreaSqm ?? 0m).Take(25).ToList(),
            UnenrichedSample: responses.Where(r => r.EnrichmentStatus != "Enriched").OrderByDescending(r => r.Ocid).Take(30).ToList(),
            RiskRules: RiskEvaluator.DescribeRules(cfg));
    }
}