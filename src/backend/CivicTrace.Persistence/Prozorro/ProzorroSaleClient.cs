using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Features.Rentals.Models;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace CivicTrace.Persistence.Prozorro;

public sealed class ProzorroSaleClient(HttpClient httpClient, ILogger<ProzorroSaleClient> logger) : IProzorroSaleClient
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public Task<ErrorOr<List<SaleRegistryObjectDto>>> GetRegistryObjectsByDateModifiedAsync(
        DateTimeOffset from,
        int limit = 100,
        CancellationToken ct = default)
    {
        var iso = from.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
        return GetListAsync<SaleRegistryObjectDto>($"registry/objects/search/byDateModified/{iso}?limit={limit}", ct);
    }

    public Task<ErrorOr<List<SaleProcedureDto>>> GetProceduresByDateModifiedAsync(
        DateTimeOffset from,
        int limit = 100,
        CancellationToken ct = default)
    {
        var iso = from.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'", CultureInfo.InvariantCulture);
        return GetListAsync<SaleProcedureDto>($"search/byDateModified/{iso}?limit={limit}", ct);
    }

    private async Task<ErrorOr<List<T>>> GetListAsync<T>(string path, CancellationToken ct)
    {
        try
        {
            using var response = await ProzorroHttpRetry.GetAsync(httpClient, path, ct);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return Error.Failure("ProzorroSale.RateLimited", "Prozorro.Sale API rate limit reached.");

            if (!response.IsSuccessStatusCode)
                return Error.Failure("ProzorroSale.Unavailable", $"Prozorro.Sale API status {(int)response.StatusCode}");

            var data = await response.Content.ReadFromJsonAsync<List<T>>(JsonOpts, ct);
            return data ?? [];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Prozorro.Sale request failed for {Path}", path);
            return Error.Failure("ProzorroSale.Error", ex.Message);
        }
    }
}
