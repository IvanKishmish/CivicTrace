using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Features.Procurements.Common;
using CivicTrace.Domain.Enums;
using ErrorOr;
using FluentValidation;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Features.Analytics.Queries.GetAnalytics;

public sealed class GetAnalyticsQueryHandler(
    IAppDbContext dbContext,
    IValidator<GetAnalyticsQuery> validator)
    : IQueryHandler<GetAnalyticsQuery, ErrorOr<AnalyticsResponse>>
{
    private static readonly string[] Notes =
    [
        "Показники описують, що відомо з оприлюднених даних, і не оцінюють законність чи доцільність закупівель.",
        "Суми не складаються в одне число: окремо «з ПДВ», «без ПДВ» і «ПДВ не вказано».",
        "Середня кількість пропозицій рахується лише там, де сторінка закупівлі показує повний реєстр пропозицій.",
        "Закупівля без використання електронної системи — це спосіб проведення, а не висновок про порушення."
    ];

    public async ValueTask<ErrorOr<AnalyticsResponse>> Handle(GetAnalyticsQuery query, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(query, ct);

        if (!validationResult.IsValid)
            return validationResult.Errors
                .Select(e => Error.Validation(e.PropertyName, e.ErrorMessage))
                .ToList();

        var raw = await dbContext.Procurements
            .AsNoTracking()
            .Select(p => new
            {
                p.ExternalId,
                BuyerEdrpou = p.Buyer.Edrpou,
                BuyerName = p.Buyer.Name,
                CpvCode = p.Cpv.Code,
                CpvName = p.Cpv.Name,
                p.Method,
                p.Status,
                p.BidsListComplete,
                ExpectedValue = p.ExpectedAmount != null ? p.ExpectedAmount.Value : (decimal?)null,
                ExpectedVat = p.ExpectedAmount != null ? p.ExpectedAmount.VatIncluded : null,
                ContractValue = p.ContractAmount != null ? p.ContractAmount.Value : (decimal?)null,
                ContractVat = p.ContractAmount != null ? p.ContractAmount.VatIncluded : null,
                BidCount = p.Bids.Count(),
                MissingCount = p.DataGaps.Count(g => g.Kind == DataGapKind.Missing),
                NeedsCheckCount = p.DataGaps.Count(g => g.Kind == DataGapKind.NeedsCheck),
                EventDates = p.Events.Where(e => e.Date != null).Select(e => e.Date).ToList()
            })
            .ToListAsync(ct);

        var rows = raw
            .Select(x => new Row(
                x.ExternalId,
                x.BuyerEdrpou,
                x.BuyerName,
                x.CpvCode,
                x.CpvName,
                x.Method,
                x.Status,
                x.BidsListComplete,
                x.ExpectedValue,
                x.ExpectedVat,
                x.ContractValue,
                x.ContractVat,
                x.BidCount,
                x.MissingCount,
                x.NeedsCheckCount,
                x.EventDates.Min()?.Year))
            .ToList();

        var groups = rows
            .GroupBy(r => KeyOf(query.GroupBy, r).Key)
            .Select(g => BuildGroup(g.Key, KeyOf(query.GroupBy, g.First()).Label, g.ToList()))
            .OrderByDescending(g => g.ProcurementsCount)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        var summary = BuildGroup("all", "Усі закупівлі", rows);

        return new AnalyticsResponse(query.GroupBy, summary, groups, Notes);
    }

    private static (string Key, string Label) KeyOf(string groupBy, Row row) => groupBy switch
    {
        "buyer" => (row.BuyerEdrpou, row.BuyerName),
        "cpv" => (row.CpvCode, row.CpvName),
        "year" => row.Year is { } year ? (year.ToString(), year.ToString()) : ("unknown", "Рік невідомий"),
        "method" => MethodKey(row.Method),
        "status" => (ProcurementMapper.StatusCode(row.Status), ProcurementMapper.StatusLabel(row.Status)),
        _ => throw new ArgumentOutOfRangeException(nameof(groupBy), groupBy, null)
    };

    private static (string Key, string Label) MethodKey(ProcurementMethod method) => method switch
    {
        ProcurementMethod.NoElectronicSystem => ("no_electronic_system", ProcurementMapper.MethodLabel(method)),
        ProcurementMethod.RequestForQuotations => ("request_for_quotations", ProcurementMapper.MethodLabel(method)),
        ProcurementMethod.OpenTendersWithSpecifics => ("open_tenders_with_specifics", ProcurementMapper.MethodLabel(method)),
        _ => throw new ArgumentOutOfRangeException(nameof(method), method, null)
    };

    private static AnalyticsGroup BuildGroup(string key, string label, IReadOnlyList<Row> rows)
    {
        var completeLists = rows.Where(r => r.BidsListComplete).ToList();

        return new AnalyticsGroup(
            Key: key,
            Label: label,
            ProcurementsCount: rows.Count,
            ContractSignedCount: rows.Count(r => r.Status == ProcurementStatus.ContractSigned),
            NotHeldCount: rows.Count(r => r.Status == ProcurementStatus.NotHeld),
            NoElectronicSystemCount: rows.Count(r => r.Method == ProcurementMethod.NoElectronicSystem),
            BidsListCompleteCount: completeLists.Count,
            AverageBidsInCompleteLists: completeLists.Count == 0 ? null : completeLists.Average(r => r.BidCount),
            ExpectedAmount: Totals(rows.Select(r => (r.ExpectedValue, r.ExpectedVat))),
            ContractAmount: Totals(rows.Select(r => (r.ContractValue, r.ContractVat))),
            RecordsWithMissingData: rows.Count(r => r.MissingCount > 0),
            AverageMissingDataItems: rows.Count == 0 ? 0 : rows.Average(r => r.MissingCount),
            RecordsNeedingCheck: rows.Count(r => r.NeedsCheckCount > 0));
    }

    private static AmountTotals Totals(IEnumerable<(decimal? Value, bool? Vat)> amounts)
    {
        decimal withVat = 0;
        decimal withoutVat = 0;
        decimal unspecified = 0;

        foreach (var (value, vat) in amounts)
        {
            if (value is not { } amount)
                continue;

            if (vat is true)
                withVat += amount;
            else if (vat is false)
                withoutVat += amount;
            else
                unspecified += amount;
        }

        return new AmountTotals(withVat, withoutVat, unspecified);
    }

    private sealed record Row(
        string ExternalId,
        string BuyerEdrpou,
        string BuyerName,
        string CpvCode,
        string CpvName,
        ProcurementMethod Method,
        ProcurementStatus Status,
        bool BidsListComplete,
        decimal? ExpectedValue,
        bool? ExpectedVat,
        decimal? ContractValue,
        bool? ContractVat,
        int BidCount,
        int MissingCount,
        int NeedsCheckCount,
        int? Year);
}