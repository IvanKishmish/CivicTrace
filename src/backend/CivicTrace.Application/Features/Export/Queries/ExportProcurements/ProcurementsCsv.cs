using System.Globalization;
using System.Text;
using CivicTrace.Application.Features.Procurements.Common;

namespace CivicTrace.Application.Features.Export.Queries.ExportProcurements;

/// <summary>CSV за RFC 4180 (роздільник — кома). Текстові поля захищено від формул Excel: значення, що починаються з = + - @, отримують префікс '.</summary>
public static class ProcurementsCsv
{
    private static readonly (string Header, Func<ProcurementResponse, string> Value)[] Columns =
    [
        ("id", r => Text(r.Id)),
        ("apiId", r => Text(r.ApiId)),
        ("planId", r => Text(r.PlanId)),
        ("title", r => Text(r.Title)),
        ("kind", r => Text(r.Kind)),
        ("cpvCode", r => Text(r.CpvCode)),
        ("cpvName", r => Text(r.CpvName)),
        ("procurementType", r => Text(r.ProcurementType)),
        ("buyer", r => Text(r.Buyer)),
        ("buyerEdrpou", r => Text(r.BuyerEdrpou)),
        ("contractor", r => Text(r.Contractor)),
        ("contractorEdrpou", r => Text(r.ContractorEdrpou)),
        ("expectedAmount", r => Number(r.ExpectedAmount)),
        ("expectedVatIncluded", r => Flag(r.ExpectedVatIncluded)),
        ("contractAmount", r => Number(r.ContractAmount)),
        ("contractVatIncluded", r => Flag(r.ContractVatIncluded)),
        ("currency", r => Text(r.Currency)),
        ("status", r => Text(r.Status)),
        ("sourceStatus", r => Text(r.SourceStatus)),
        ("executionStatus", r => Text(r.ExecutionStatus)),
        ("address", r => Text(r.Address)),
        ("placeType", r => Text(r.PlaceType)),
        ("latitude", r => Number(r.Latitude)),
        ("longitude", r => Number(r.Longitude)),
        ("coordinatesStatus", r => Text(r.CoordinatesStatus)),
        ("periodStart", r => Date(r.PeriodStart)),
        ("periodEnd", r => Date(r.PeriodEnd)),
        ("bidsListComplete", r => Flag(r.BidsListComplete)),
        ("bidsCount", r => r.BidsListComplete ? r.Bids.Count.ToString(CultureInfo.InvariantCulture) : string.Empty),
        ("eventsCount", r => r.Events.Count.ToString(CultureInfo.InvariantCulture)),
        ("firstEventDate", r => Date(r.Events.Where(e => e.Date is not null).Select(e => e.Date).Min())),
        ("missingDataCount", r => r.MissingData.Count.ToString(CultureInfo.InvariantCulture)),
        ("needsCheckCount", r => r.NeedsCheck.Count.ToString(CultureInfo.InvariantCulture)),
        ("sourceUrl", r => Text(r.SourceUrl)),
        ("verifiedAt", r => Date(r.VerifiedAt))
    ];

    public static string Build(IEnumerable<ProcurementResponse> procurements)
    {
        var builder = new StringBuilder();

        builder.Append(string.Join(',', Columns.Select(c => c.Header))).Append("\r\n");

        foreach (var procurement in procurements)
            builder.Append(string.Join(',', Columns.Select(c => c.Value(procurement)))).Append("\r\n");

        return builder.ToString();
    }

    private static string Text(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if ("=+-@\t\r".Contains(value[0]))
            value = "'" + value;

        return Quote(value);
    }

    private static string Number(decimal? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Number(double? value)
        => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Flag(bool? value)
        => value is null ? string.Empty : value.Value ? "true" : "false";

    private static string Date(DateOnly? value)
        => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Quote(string value)
        => value.IndexOfAny([',', '"', '\n', '\r']) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}