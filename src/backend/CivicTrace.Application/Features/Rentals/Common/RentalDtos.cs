using System.Text.Json;
using CivicTrace.Domain.Entities;

namespace CivicTrace.Application.Features.Rentals.Common;

public sealed record RentalAuctionDto(
    string AuctionId,
    string ApiId,
    string Title,
    string SellingMethod,
    string Status,
    string StatusLabel,
    int? TenderAttempts,
    decimal? StartPrice,
    decimal? FinalPrice,
    decimal? MinimalStep,
    decimal? AreaSqm,
    decimal? PricePerSqmStart,
    decimal? PricePerSqmFinal,
    int BidsCount,
    decimal? PriceIncreasePct,
    DateTimeOffset? DatePublished,
    DateTimeOffset? AuctionDate,
    DateTimeOffset? DateCompleted,
    DateTimeOffset? ContractDateSigned,
    string SourceUrl,
    int RiskScore,
    IReadOnlyList<RiskFlagDto> RiskFlags);

public sealed record RentalObjectResponse(
    string Ocid,
    string? ApiId,
    string Title,
    string RawTitle,
    string ObjectType,
    string CanonicalUrl,
    string OriginalUrl,
    string EnrichmentStatus,
    int? CreatedYear,
    DateTimeOffset? DatePublished,
    DateTimeOffset? DateModified,
    string? Address,
    string? District,
    double? Latitude,
    double? Longitude,
    bool HasCoordinates,
    string? CoordinatesStatus,
    decimal? AreaSqm,
    string StatusCode,
    string StatusLabel,
    string? SourceStatus,
    string ListType,
    string? PropertyOwnerName,
    string? PropertyOwnerEdrpou,
    string? SellingEntityName,
    string? SellingEntityEdrpou,
    decimal? MarketValue,
    decimal? SecondListAbsoluteValue,
    string? LeaseDuration,
    bool? SubleaseAllowed,
    string? CurrentContractNumber,
    string? CurrentContractMethod,
    decimal? CurrentContractValue,
    DateTimeOffset? CurrentContractStart,
    DateTimeOffset? CurrentContractEnd,
    string? CurrentTenantLabel,
    string? DataDefectsNotes,
    DateTimeOffset? LastSyncedAt,
    int RiskScore,
    IReadOnlyList<RiskFlagDto> RiskFlags,
    IReadOnlyList<RentalAuctionDto> Auctions);

public static class RentalMapper
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static RentalObjectResponse ToResponse(RentalObject o, decimal? medianRate, RiskRulesConfig cfg)
    {
        var objFlags = RiskEvaluator.EvaluateObject(o, medianRate, cfg);
        var score = objFlags.Sum(f => f.Weight);

        var auctions = o.Auctions
            .OrderByDescending(a => a.DatePublished ?? a.AuctionDate)
            .Select(ToAuctionDto)
            .ToList();

        return new RentalObjectResponse(
            o.Ocid,
            o.ApiId,
            o.NormalizedTitle,
            o.RawTitle,
            o.ObjectType,
            o.CanonicalUrl,
            o.OriginalUrl,
            o.EnrichmentStatus,
            o.CreatedYear,
            o.DatePublished,
            o.DateModified,
            o.Address,
            o.District,
            o.Latitude,
            o.Longitude,
            o.Latitude is not null && o.Longitude is not null,
            o.CoordinatesStatus,
            o.AreaSqm,
            o.StatusCode,
            o.StatusLabel,
            o.SourceStatus,
            o.ListType,
            o.PropertyOwnerName,
            o.PropertyOwnerEdrpou,
            o.SellingEntityName,
            o.SellingEntityEdrpou,
            o.MarketValue,
            o.SecondListAbsoluteValue,
            o.LeaseDuration,
            o.SubleaseAllowed,
            o.CurrentContractNumber,
            o.CurrentContractMethod,
            o.CurrentContractValue,
            o.CurrentContractStart,
            o.CurrentContractEnd,
            o.CurrentTenantLabel,
            o.DataDefectsNotes,
            o.LastSyncedAt,
            score,
            objFlags,
            auctions);
    }

    public static RentalAuctionDto ToAuctionDto(RentalAuction a)
    {
        List<RiskFlagDto> flags = [];
        if (!string.IsNullOrWhiteSpace(a.RiskFlagsJson))
        {
            try
            {
                flags = JsonSerializer.Deserialize<List<RiskFlagDto>>(a.RiskFlagsJson, JsonOpts) ?? [];
            }
            catch { }
        }

        return new RentalAuctionDto(
            a.AuctionId,
            a.ApiId,
            a.Title,
            a.SellingMethod,
            a.Status,
            a.StatusLabel,
            a.TenderAttempts,
            a.StartPrice,
            a.FinalPrice,
            a.MinimalStep,
            a.AreaSqm,
            a.PricePerSqmStart,
            a.PricePerSqmFinal,
            a.BidsCount,
            a.PriceIncreasePct,
            a.DatePublished,
            a.AuctionDate,
            a.DateCompleted,
            a.ContractDateSigned,
            a.SourceUrl,
            a.RiskScore,
            flags);
    }

    public static decimal? ComputeMedianRate(IEnumerable<RentalAuction> auctions)
    {
        var rates = auctions
            .Where(a => a.Status == "complete" && a.PricePerSqmFinal is > 0)
            .Select(a => a.PricePerSqmFinal!.Value)
            .OrderBy(x => x)
            .ToList();

        if (rates.Count == 0) return null;
        var mid = rates.Count / 2;
        return rates.Count % 2 == 0
            ? Math.Round((rates[mid - 1] + rates[mid]) / 2m, 2)
            : rates[mid];
    }
}