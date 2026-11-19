using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Common.Options;
using CivicTrace.Application.Features.Prozorro.Commands.ImportProcurement;
using CivicTrace.Application.Features.Prozorro.Models;
using CivicTrace.Domain.Enums;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CivicTrace.Application.Features.Prozorro.Commands.SyncPoltava;

public sealed class SyncPoltavaCommandHandler(
    IProzorroClient prozorroClient,
    IGeocoderService geocoder,
    IAppDbContext dbContext,
    IMediator mediator,
    IOptions<ProzorroOptions> prozorroOptions,
    ILogger<SyncPoltavaCommandHandler> logger)
    : ICommandHandler<SyncPoltavaCommand, ErrorOr<SyncPoltavaResponse>>
{
    public async ValueTask<ErrorOr<SyncPoltavaResponse>> Handle(
        SyncPoltavaCommand command,
        CancellationToken ct = default)
    {
        var opts = prozorroOptions.Value;
        var maxPages = Math.Clamp(command.MaxPages ?? opts.SyncMaxPages, 1, 50);
        var targetNew = Math.Clamp(command.TargetNewCount ?? opts.SyncTargetNewCount, 1, 100);
        var localityFilter = opts.PoltavaLocalityFilter;

        // 1. Геокодуємо існуючі записи з адресою без координат (наприклад, із сид-файлу)
        var unmapped = await dbContext.Procurements
            .Include(p => p.DataGaps)
            .Where(p => p.Place.Address != null && p.Place.Latitude == null)
            .ToListAsync(ct);

        var geocodedExisting = 0;
        foreach (var item in unmapped)
        {
            if (ct.IsCancellationRequested) break;

            var coords = await geocoder.GeocodePoltavaAsync(item.Place.Address!, ct);
            if (coords is not { } c) continue;

            var res = item.SetCoordinates(c.Latitude, c.Longitude, CoordinatesStatus.NeedsCheck);
            if (res.IsError) continue;

            const string note = "Координати визначено автоматично за адресою (OpenStreetMap Nominatim); команда їх не звіряла.";
            if (!item.DataGaps.Any(g => g.Kind == DataGapKind.NeedsCheck && g.Text == note))
                item.AddDataGap(DataGapKind.NeedsCheck, note);

            geocodedExisting++;
        }

        if (geocodedExisting > 0)
            await dbContext.SaveChangesAsync(ct);

        // 2. Скануємо свіжу стрічку Prozorro API по Полтаві
        var existingApiIds = (await dbContext.Procurements
                .AsNoTracking()
                .Where(p => p.ApiId != null)
                .Select(p => p.ApiId!)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingExternalIds = (await dbContext.Procurements
                .AsNoTracking()
                .Select(p => p.ExternalId)
                .ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var pagesScanned = 0;
        var matchedPoltava = 0;
        var imported = 0;
        var skippedExisting = 0;
        var failed = 0;
        string? offset = null;

        while (pagesScanned < maxPages && imported < targetNew && !ct.IsCancellationRequested)
        {
            var pageResult = await prozorroClient.GetTendersPageAsync(offset, 100, ct);
            if (pageResult.IsError)
            {
                if (pagesScanned == 0)
                    return pageResult.Errors;

                logger.LogWarning("Prozorro feed stopped at page {Page}: {Error}", pagesScanned + 1, pageResult.FirstError.Description);
                break;
            }

            pagesScanned++;
            var page = pageResult.Value;
            var items = page.Data ?? [];

            if (items.Count == 0)
                break;

            foreach (var item in items)
            {
                if (imported >= targetNew || ct.IsCancellationRequested)
                    break;

                if (!IsPoltavaTender(item, localityFilter))
                    continue;

                matchedPoltava++;

                if (existingApiIds.Contains(item.Id) ||
                    (!string.IsNullOrWhiteSpace(item.TenderID) && existingExternalIds.Contains(item.TenderID)))
                {
                    skippedExisting++;
                    continue;
                }

                var importResult = await mediator.Send(
                    new ImportProcurementCommand(item.Id, DryRun: false, ReplaceExisting: false),
                    ct);

                if (importResult.IsError)
                {
                    failed++;
                    logger.LogDebug("Skipped Poltava tender {ApiId}: {Reason}", item.Id, importResult.FirstError.Description);
                    continue;
                }

                imported++;
                existingApiIds.Add(item.Id);
                existingExternalIds.Add(importResult.Value.Procurement.Id);
            }

            var nextOffset = page.NextPage?.Offset;
            if (string.IsNullOrWhiteSpace(nextOffset) || nextOffset == offset)
                break;

            offset = nextOffset;
        }

        logger.LogInformation(
            "Poltava Prozorro sync finished: {Pages} pages scanned, {Matched} matched Poltava, {Imported} imported, {Skipped} existing, {Failed} skipped/unsupported, {Geocoded} existing geocoded",
            pagesScanned, matchedPoltava, imported, skippedExisting, failed, geocodedExisting);

        return new SyncPoltavaResponse(
            pagesScanned,
            matchedPoltava,
            imported,
            skippedExisting,
            failed,
            geocodedExisting);
    }

    private static bool IsPoltavaTender(ProzorroTenderFeedItem item, string filter)
    {
        var addr = item.ProcuringEntity?.Address;
        var locality = addr?.Locality ?? string.Empty;
        var region = addr?.Region ?? string.Empty;
        var name = item.ProcuringEntity?.Name ?? string.Empty;

        if (locality.Contains(filter, StringComparison.OrdinalIgnoreCase))
            return true;

        if (region.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
            (name.Contains("Полтавської міської", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("Полтавська міська", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("Полтавська районна", StringComparison.OrdinalIgnoreCase) ||
             name.Contains("м. Полтава", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return false;
    }
}