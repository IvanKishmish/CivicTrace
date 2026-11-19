using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Features.Export.Queries.ExportProcurements;
using CivicTrace.Application.Features.Rentals.Common;
using ErrorOr;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CivicTrace.Application.Features.Rentals.Queries.ExportRentals;

public sealed record ExportRentalsQuery(string Format = "csv") : IQuery<ErrorOr<ExportFile>>;

public sealed class ExportRentalsQueryHandler(IAppDbContext dbContext)
    : IQueryHandler<ExportRentalsQuery, ErrorOr<ExportFile>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public async ValueTask<ErrorOr<ExportFile>> Handle(ExportRentalsQuery query, CancellationToken ct = default)
    {
        var objects = await dbContext.RentalObjects.AsNoTracking().Include(r => r.Auctions).OrderByDescending(r => r.Ocid).ToListAsync(ct);
        var allAuctions = await dbContext.RentalAuctions.AsNoTracking().ToListAsync(ct);
        var medianRate = RentalMapper.ComputeMedianRate(allAuctions);
        var cfg = RiskRulesConfig.Load();

        var responses = objects.Select(o => RentalMapper.ToResponse(o, medianRate, cfg)).ToList();
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd");

        if (string.Equals(query.Format, "json", StringComparison.OrdinalIgnoreCase))
            return new ExportFile(JsonSerializer.SerializeToUtf8Bytes(responses, JsonOptions), "application/json; charset=utf-8", $"poltava-rental-objects-{stamp}.json");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(BuildCsv(responses))).ToArray();
        return new ExportFile(bytes, "text/csv; charset=utf-8", $"poltava-rental-objects-{stamp}.csv");
    }

    private static string BuildCsv(IEnumerable<RentalObjectResponse> rows)
    {
        static string Text(string? v)
        {
            if (string.IsNullOrEmpty(v)) return string.Empty;
            if ("=+-@\t\r".Contains(v[0])) v = "'" + v;
            return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;
        }

        static string Num(decimal? v) => v?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        static string Dbl(double? v) => v?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("ocid,apiId,enrichmentStatus,title,objectType,createdYear,statusCode,statusLabel,listType,areaSqm,address,district,latitude,longitude,coordinatesStatus,propertyOwnerName,propertyOwnerEdrpou,sellingEntityName,sellingEntityEdrpou,currentContractNumber,currentContractMethod,currentContractValue,auctionsCount,riskScore,riskFlags,canonicalUrl,dataDefectsNotes");

        foreach (var r in rows)
        {
            var flags = string.Join("; ", r.RiskFlags.Select(f => f.Code));
            sb.AppendLine(string.Join(',',
                Text(r.Ocid), Text(r.ApiId), Text(r.EnrichmentStatus), Text(r.Title), Text(r.ObjectType),
                r.CreatedYear?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                Text(r.StatusCode), Text(r.StatusLabel), Text(r.ListType), Num(r.AreaSqm),
                Text(r.Address), Text(r.District), Dbl(r.Latitude), Dbl(r.Longitude), Text(r.CoordinatesStatus),
                Text(r.PropertyOwnerName), Text(r.PropertyOwnerEdrpou), Text(r.SellingEntityName), Text(r.SellingEntityEdrpou),
                Text(r.CurrentContractNumber), Text(r.CurrentContractMethod), Num(r.CurrentContractValue),
                r.Auctions.Count.ToString(CultureInfo.InvariantCulture), r.RiskScore.ToString(CultureInfo.InvariantCulture),
                Text(flags), Text(r.CanonicalUrl), Text(r.DataDefectsNotes)));
        }

        return sb.ToString();
    }
}