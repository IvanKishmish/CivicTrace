using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Features.Rentals.Common;
using CivicTrace.Application.Features.Rentals.Models;
using CivicTrace.Domain.Entities;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CivicTrace.Application.Features.Rentals.Commands.SyncRentals;

public sealed record SyncRentalsCommand(int MaxBatchesPerSlice = 25) : ICommand<ErrorOr<SyncRentalsResponse>>;

public sealed record SyncRentalsResponse(
    int ObjectsEnriched,
    int AuctionsImported,
    int GeocodedCount,
    int TotalEnrichedInDb,
    int TotalNotEnrichedInDb);

public sealed partial class SyncRentalsCommandHandler(
    IProzorroSaleClient saleClient,
    IGeocoderService geocoder,
    IAppDbContext dbContext,
    ILogger<SyncRentalsCommandHandler> logger)
    : ICommandHandler<SyncRentalsCommand, ErrorOr<SyncRentalsResponse>>
{
    private const string PoltavaOwnerEdrpou = "13967034";
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public async ValueTask<ErrorOr<SyncRentalsResponse>> Handle(SyncRentalsCommand command, CancellationToken ct = default)
    {
        var objects = await dbContext.RentalObjects.ToListAsync(ct);
        var byOcid = objects.ToDictionary(o => o.Ocid, StringComparer.OrdinalIgnoreCase);

        var matchedObjects = new ConcurrentDictionary<string, SaleRegistryObjectDto>(StringComparer.OrdinalIgnoreCase);
        var slices = BuildTimeSlices(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero), DateTimeOffset.UtcNow.AddDays(1), TimeSpan.FromDays(90));

        await Parallel.ForEachAsync(slices, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (slice, token) =>
        {
            var cursor = slice.Start;
            for (var i = 0; i < command.MaxBatchesPerSlice && !token.IsCancellationRequested; i++)
            {
                var res = await saleClient.GetRegistryObjectsByDateModifiedAsync(cursor, 100, token);
                if (res.IsError || res.Value.Count == 0) break;

                DateTimeOffset? lastMod = null;
                foreach (var dto in res.Value)
                {
                    if (DateTimeOffset.TryParse(dto.DateModified, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dm))
                        lastMod = dm;

                    if (!string.IsNullOrWhiteSpace(dto.RegistryObjectId) && byOcid.ContainsKey(dto.RegistryObjectId))
                        matchedObjects[dto.RegistryObjectId] = dto;
                }

                if (res.Value.Count < 100 || lastMod is null || lastMod.Value >= slice.End)
                    break;

                cursor = lastMod.Value.AddMilliseconds(1);
            }
        });

        var enrichedNow = 0;
        foreach (var (ocid, dto) in matchedObjects)
        {
            if (!byOcid.TryGetValue(ocid, out var entity)) continue;
            ApplyObjectDto(entity, dto);
            enrichedNow++;
        }

        var geocodedNow = 0;
        foreach (var obj in objects.Where(o => o.Latitude == null && !string.IsNullOrWhiteSpace(o.Address)).Take(40))
        {
            if (ct.IsCancellationRequested) break;
            var coords = await geocoder.GeocodePoltavaAsync(obj.Address!, ct);
            if (coords is { } c)
            {
                obj.SetGeocodedCoordinates(c.Latitude, c.Longitude);
                geocodedNow++;
            }
        }

        await dbContext.SaveChangesAsync(ct);

        var matchedAuctions = new ConcurrentDictionary<string, SaleProcedureDto>(StringComparer.OrdinalIgnoreCase);
        var auctionSlices = BuildTimeSlices(new DateTimeOffset(2021, 1, 1, 0, 0, 0, TimeSpan.Zero), DateTimeOffset.UtcNow.AddDays(1), TimeSpan.FromDays(120));

        await Parallel.ForEachAsync(auctionSlices, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (slice, token) =>
        {
            var cursor = slice.Start;
            for (var i = 0; i < command.MaxBatchesPerSlice && !token.IsCancellationRequested; i++)
            {
                var res = await saleClient.GetProceduresByDateModifiedAsync(cursor, 100, token);
                if (res.IsError || res.Value.Count == 0) break;

                DateTimeOffset? lastMod = null;
                foreach (var dto in res.Value)
                {
                    if (DateTimeOffset.TryParse(dto.DateModified, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dm))
                        lastMod = dm;

                    var isLease = dto.SellingMethod?.Contains("PropertyLease", StringComparison.OrdinalIgnoreCase) == true;
                    if (!isLease) continue;

                    var roid = dto.RegistryObjectId ?? dto.LotId;
                    var isPoltavaOrg = dto.RelatedOrganizations?.SellingEntity?.Identifier?.Id == PoltavaOwnerEdrpou ||
                                       dto.RelatedOrganizations?.PropertyOwner?.Identifier?.Id == PoltavaOwnerEdrpou;

                    if ((roid is not null && byOcid.ContainsKey(roid)) || isPoltavaOrg)
                    {
                        var key = dto.AuctionId ?? dto.Id;
                        matchedAuctions[key] = dto;
                    }
                }

                if (res.Value.Count < 100 || lastMod is null || lastMod.Value >= slice.End)
                    break;

                cursor = lastMod.Value.AddMilliseconds(1);
            }
        });

        var existingAuctions = await dbContext.RentalAuctions.ToListAsync(ct);
        var existingAuctionsById = existingAuctions.ToDictionary(a => a.AuctionId, StringComparer.OrdinalIgnoreCase);

        var newAuctions = new List<RentalAuction>();
        var refreshedAuctions = 0;
        foreach (var (auctionKey, dto) in matchedAuctions)
        {
            var roid = dto.RegistryObjectId ?? dto.LotId;
            RentalObject? parent = roid is not null && byOcid.TryGetValue(roid, out var found) ? found : null;

            var item = dto.Items?.FirstOrDefault();
            decimal? area = item?.ReProps?.TotalObjectArea is > 0 ? (decimal)item.ReProps.TotalObjectArea.Value
                : item?.Quantity is > 0 ? (decimal)item.Quantity.Value
                : parent?.AreaSqm;

            var startPrice = dto.Value?.Amount;
            var activeContract = dto.Contracts?.FirstOrDefault(c => c.Status == "active") ?? dto.Contracts?.FirstOrDefault();
            var activeAward = dto.Awards?.FirstOrDefault(a => a.Status == "active") ?? dto.Awards?.FirstOrDefault();
            var finalPrice = activeContract?.Value?.Amount ?? activeAward?.Value?.Amount;
            var winnerMasked = MaskParticipant(activeContract?.Buyers?.FirstOrDefault() ?? activeAward?.Buyers?.FirstOrDefault());

            var auction = RentalAuction.Create(
                auctionId: auctionKey, apiId: dto.Id, rentalObjectId: parent?.Id, registryObjectOcid: roid,
                title: dto.Title?.UkUa?.Trim() ?? parent?.NormalizedTitle ?? auctionKey,
                sellingMethod: dto.SellingMethod ?? "PropertyLease", status: dto.Status ?? "unknown",
                statusLabel: MapAuctionStatusLabel(dto.Status), tenderAttempts: dto.TenderAttempts,
                startPrice: startPrice, finalPrice: finalPrice, minimalStep: dto.MinimalStep?.Amount,
                areaSqm: area, bidsCount: dto.Bids?.Count ?? 0, winnerKeyMasked: winnerMasked,
                datePublished: ParseDate(dto.DatePublished), auctionDate: ParseDate(dto.AuctionPeriod?.StartDate),
                dateCompleted: ParseDate(dto.DateCompleted), contractDateSigned: ParseDate(activeContract?.DateSigned));

            if (existingAuctionsById.TryGetValue(auctionKey, out var existingAuction))
            {
                existingAuction.RefreshFrom(auction);
                refreshedAuctions++;
            }
            else
            {
                newAuctions.Add(auction);
                existingAuctionsById.Add(auctionKey, auction);
            }

            if (parent is not null && dto.Status?.StartsWith("active", StringComparison.OrdinalIgnoreCase) == true && parent.StatusCode != "rented")
            {
                parent.ApplyEnrichment(
                    parent.ApiId, parent.NormalizedTitle, parent.DatePublished, parent.DateModified,
                    parent.Address, parent.District, parent.Latitude, parent.Longitude, parent.CoordinatesStatus,
                    parent.AreaSqm ?? area, "on_auction", "На аукціоні", dto.Status, parent.ListType,
                    parent.PropertyOwnerName, parent.PropertyOwnerEdrpou, parent.SellingEntityName, parent.SellingEntityEdrpou,
                    parent.MarketValue, parent.SecondListAbsoluteValue, parent.LeaseDuration, parent.SubleaseAllowed,
                    parent.CurrentContractNumber, parent.CurrentContractMethod, parent.CurrentContractValue,
                    parent.CurrentContractStart, parent.CurrentContractEnd, parent.CurrentTenantLabel);
            }
        }

        if (newAuctions.Count > 0 || refreshedAuctions > 0)
        {
            if (newAuctions.Count > 0)
                dbContext.RentalAuctions.AddRange(newAuctions);
            await dbContext.SaveChangesAsync(ct);
        }

        var allAuctions = await dbContext.RentalAuctions.ToListAsync(ct);
        var medianRate = RentalMapper.ComputeMedianRate(allAuctions);
        var cfg = RiskRulesConfig.Load();

        foreach (var auction in allAuctions)
        {
            RentalObject? parent = auction.RegistryObjectOcid is not null && byOcid.TryGetValue(auction.RegistryObjectOcid, out var p) ? p : null;
            var (score, flags) = RiskEvaluator.EvaluateAuction(auction, parent, medianRate, cfg);
            auction.UpdateRisk(score, JsonSerializer.Serialize(flags, JsonOpts));
        }

        await dbContext.SaveChangesAsync(ct);

        var totalEnriched = objects.Count(o => o.EnrichmentStatus == "Enriched");
        logger.LogInformation("Rental sync finished: {Enriched} enriched now ({TotalEnriched}/{Total}), {Auctions} new auctions, {Geo} geocoded",
            enrichedNow, totalEnriched, objects.Count, newAuctions.Count, geocodedNow);

        return new SyncRentalsResponse(enrichedNow, newAuctions.Count, geocodedNow, totalEnriched, objects.Count - totalEnriched);
    }

    private static void ApplyObjectDto(RentalObject entity, SaleRegistryObjectDto dto)
    {
        var item = dto.RegistryObjectItems?.FirstOrDefault();
        var addrObj = item?.Placing?.Address;
        var street = addrObj?.StreetAddress?.UkUa?.Trim();
        var locality = addrObj?.Locality?.UkUa?.Trim();
        var address = string.Join(", ", new[] { addrObj?.PostalCode, locality, street }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (string.IsNullOrWhiteSpace(address)) address = null;

        double? lat = null;
        double? lng = null;
        string? coordStatus = null;
        if (double.TryParse(item?.Placing?.Location?.Latitude, CultureInfo.InvariantCulture, out var parsedLat) &&
            double.TryParse(item?.Placing?.Location?.Longitude, CultureInfo.InvariantCulture, out var parsedLng))
        {
            lat = parsedLat;
            lng = parsedLng;
            coordStatus = "VerifiedFromApi";
        }

        var area = item?.ReProps?.TotalObjectArea is > 0 ? (decimal?)Math.Round(item.ReProps.TotalObjectArea.Value, 2) : null;
        var sourceStatus = dto.StatusesDecisions?.StatusInList;
        var (statusCode, statusLabel) = MapObjectStatus(sourceStatus, dto.Contracts);
        var listType = dto.ValuesCharacteristics?.SecondListAbsoluteValue?.Amount is > 0 ? "second"
            : sourceStatus is not null ? "first" : "unknown";

        var owner = dto.RelatedOrganizations?.PropertyOwner;
        var seller = dto.RelatedOrganizations?.SellingEntity;
        var contract = dto.Contracts?.FirstOrDefault(c => c.Status == "active") ?? dto.Contracts?.FirstOrDefault();
        var tenantOrg = contract?.Buyers?.FirstOrDefault() ?? dto.RelatedOrganizations?.CurrentTenants?.FirstOrDefault();

        entity.ApplyEnrichment(
            dto.Id, dto.Title?.UkUa, ParseDate(dto.DatePublished), ParseDate(dto.DateModified),
            address, ExtractDistrict(street), lat, lng, coordStatus, area,
            statusCode, statusLabel, sourceStatus, listType,
            owner?.Identifier?.LegalName?.UkUa ?? owner?.Name?.UkUa, owner?.Identifier?.Id,
            seller?.Identifier?.LegalName?.UkUa ?? seller?.Name?.UkUa, seller?.Identifier?.Id,
            dto.ValuesCharacteristics?.MarketValue?.Amount, dto.ValuesCharacteristics?.SecondListAbsoluteValue?.Amount,
            dto.LeaseRules?.LeaseDuration, dto.LeaseRules?.SubleaseAllowed,
            contract?.ContractNumber, contract?.ContractMethod, contract?.Value?.Amount,
            ParseDate(contract?.ContractTime?.DateFrom ?? contract?.DateSigned), ParseDate(contract?.ContractTime?.DateTill),
            MaskParticipant(tenantOrg));
    }

    private static string? MaskParticipant(SaleOrganization? org)
    {
        if (org is null) return null;
        var scheme = org.Identifier?.Scheme;
        var id = org.Identifier?.Id?.Trim();
        var name = org.Identifier?.LegalName?.UkUa?.Trim() ?? org.Name?.UkUa?.Trim();

        if (string.Equals(scheme, "UA-EDR", StringComparison.OrdinalIgnoreCase) && id is { Length: 8 } && id.All(char.IsAsciiDigit))
            return string.IsNullOrWhiteSpace(name) || name.Contains('*') ? $"Юридична особа (ЄДРПОУ {id})" : $"{name} (ЄДРПОУ {id})";

        return "Фізична особа / ФОП (знеособлено за ТЗ)";
    }

    private static (string Code, string Label) MapObjectStatus(string? raw, List<SaleContract>? contracts)
    {
        if (raw is "sold" or "rented" || (contracts?.Any(c => c.Status == "active") == true)) return ("rented", "В оренді");
        if (raw is "free" or "available" or "active") return ("free", "Вільне");
        if (raw is "auction" or "tendering") return ("on_auction", "На аукціоні");
        return ("unknown", string.IsNullOrWhiteSpace(raw) ? "Статус не вказано" : $"Статус: {raw}");
    }

    private static string MapAuctionStatusLabel(string? status) => status switch
    {
        "complete" => "Завершено (договір)",
        "unsuccessful" => "Не відбувся",
        "cancelled" => "Відмінено",
        var s when s?.StartsWith("active", StringComparison.OrdinalIgnoreCase) == true => "Активний лот",
        _ => status ?? "Невідомо"
    };

    private static string? ExtractDistrict(string? street)
    {
        if (string.IsNullOrWhiteSpace(street)) return null;
        var m = DistrictRegex().Match(street);
        return m.Success ? m.Value.Trim() : null;
    }

    private static DateTimeOffset? ParseDate(string? raw)
        => DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var d) ? d : null;

    private static List<(DateTimeOffset Start, DateTimeOffset End)> BuildTimeSlices(DateTimeOffset from, DateTimeOffset to, TimeSpan step)
    {
        var list = new List<(DateTimeOffset, DateTimeOffset)>();
        var cur = from;
        while (cur < to)
        {
            var next = cur + step;
            if (next > to) next = to;
            list.Add((cur, next));
            cur = next;
        }
        return list;
    }

    [GeneratedRegex(@"(Шевченківський|Київський|Подільський)\s+район", RegexOptions.IgnoreCase)]
    private static partial Regex DistrictRegex();
}
