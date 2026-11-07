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
        "Закупівля без використання електронної системи — це спосіб проведення, а не висновок про порушення.",
        "Суми фінансування (fundingAmount) беруться з плану закупівлі; одна закупівля може мати кілька джерел фінансування."
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
                EventDates = p.Events.Where(e => e.Date != null).Select(e => e.Date).ToList(),
                Funding = p.FundingSources.Select(f => new { f.Source, f.ExpenditureCode, f.Amount }).ToList()
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
                x.EventDates.Min()?.Year,
                x.Funding.Select(f => new FundingRow(f.Source, f.ExpenditureCode, f.Amount)).ToList()))
            .ToList();

        var groups = (query.GroupBy is "funding" or "expenditure")
            ? BuildFundingGroups(query.GroupBy, rows)
            : BuildRegularGroups(query.GroupBy, rows);

        var summary = BuildGroup("all", "Усі закупівлі", rows);

        return new AnalyticsResponse(query.GroupBy, summary, groups, Notes);
    }

    private static List<AnalyticsGroup> BuildRegularGroups(string groupBy, IReadOnlyList<Row> rows)
        => rows
            .GroupBy(r => KeyOf(groupBy, r).Key)
            .Select(g => BuildGroup(g.Key, KeyOf(groupBy, g.First()).Label, g.ToList()))
            .OrderByDescending(g => g.ProcurementsCount)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// Закупівля потрапляє в групу один раз, навіть якщо в ній кілька рядків фінансування з тим самим ключем.
    /// FundingAmount — сума фінансування за ключем.
    /// </summary>
    private static List<AnalyticsGroup> BuildFundingGroups(string groupBy, IReadOnlyList<Row> rows)
    {
        var items = new List<(string Key, string Label, Row Row, decimal Amount)>();

        foreach (var row in rows)
        {
            if (row.Funding.Count == 0)
            {
                items.Add((
                    "unknown",
                    groupBy == "funding" ? "Джерело фінансування невідоме" : "Код видатків невідомий",
                    row,
                    0m));
                continue;
            }

            foreach (var funding in row.Funding)
            {
                var key = groupBy == "funding" ? funding.Source : funding.ExpenditureCode ?? "unknown";
                var label = groupBy == "funding" ? funding.Source : funding.ExpenditureCode ?? "Код видатків невідомий";

                items.Add((key, label, row, funding.Amount));
            }
        }

        return items
            .GroupBy(i => i.Key)
            .Select(g => BuildGroup(
                g.Key,
                g.First().Label,
                g.Select(i => i.Row).DistinctBy(r => r.ExternalId).ToList(),
                g.Sum(i => i.Amount)))
            .OrderByDescending(g => g.ProcurementsCount)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToList();
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

    private static AnalyticsGroup BuildGroup(string key, string label, IReadOnlyList<Row> rows, decimal? fundingAmount = null)
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
            RecordsNeedingCheck: rows.Count(r => r.NeedsCheckCount > 0),
            FundingAmount: fundingAmount);
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

    private sealed record FundingRow(string Source, string? ExpenditureCode, decimal Amount);

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
        int? Year,
        IReadOnlyList<FundingRow> Funding);
}