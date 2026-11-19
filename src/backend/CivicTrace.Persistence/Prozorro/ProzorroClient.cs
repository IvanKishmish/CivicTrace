using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CivicTrace.Application.Common.Interfaces;
using CivicTrace.Application.Features.Prozorro.Models;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace CivicTrace.Persistence.Prozorro;

public sealed class ProzorroClient(HttpClient httpClient, ILogger<ProzorroClient> logger) : IProzorroClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ErrorOr<ProzorroTender>> GetTenderAsync(string apiId, CancellationToken ct = default)
        => GetDataAsync<ProzorroTenderEnvelope, ProzorroTender>($"tenders/{apiId}", "Tender", apiId, e => e.Data, ct);

    public Task<ErrorOr<ProzorroPlan>> GetPlanAsync(string planApiId, CancellationToken ct = default)
        => GetDataAsync<ProzorroPlanEnvelope, ProzorroPlan>($"plans/{planApiId}", "Plan", planApiId, e => e.Data, ct);

    public async Task<ErrorOr<ProzorroTenderFeedEnvelope>> GetTendersPageAsync(
        string? offset = null,
        int limit = 100,
        CancellationToken ct = default)
    {
        var path = $"tenders?descending=1&limit={limit}&opt_fields=procuringEntity,status,tenderID";
        if (!string.IsNullOrWhiteSpace(offset))
            path += $"&offset={Uri.EscapeDataString(offset)}";

        HttpResponseMessage response;
        try
        {
            response = await httpClient.GetAsync(path, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Prozorro feed request failed");
            return Error.Failure("Prozorro.Unreachable", "The Prozorro API is unreachable.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Error.Failure("Prozorro.Timeout", "The Prozorro API feed timed out.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return Error.Failure("Prozorro.RateLimited", "The Prozorro API is rate limiting requests.");

            if (!response.IsSuccessStatusCode)
                return Error.Failure("Prozorro.Unavailable", $"The Prozorro API returned status {(int)response.StatusCode}.");

            var envelope = await response.Content.ReadFromJsonAsync<ProzorroTenderFeedEnvelope>(JsonOptions, ct);
            if (envelope is null)
                return Error.Failure("Prozorro.InvalidResponse", "The Prozorro feed response could not be parsed.");

            return envelope;
        }
    }

    private async Task<ErrorOr<TData>> GetDataAsync<TEnvelope, TData>(
        string path,
        string kind,
        string id,
        Func<TEnvelope, TData?> selector,
        CancellationToken ct)
        where TData : class
    {
        HttpResponseMessage response;

        try
        {
            response = await ProzorroHttpRetry.GetAsync(httpClient, path, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Prozorro API request failed for {Kind} {Id}", kind, id);
            return Error.Failure("Prozorro.Unreachable", "The Prozorro API is unreachable. Try again later.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Error.Failure("Prozorro.Timeout", "The Prozorro API did not respond in time. Try again later.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                return Error.NotFound($"Prozorro.{kind}NotFound", $"{kind} '{id}' was not found in the Prozorro API.");

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return Error.Failure("Prozorro.RateLimited", "The Prozorro API is rate limiting requests. Try again in a minute.");

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Prozorro API returned {StatusCode} for {Kind} {Id}", (int)response.StatusCode, kind, id);

                return Error.Failure(
                    "Prozorro.Unavailable",
                    $"The Prozorro API returned status {(int)response.StatusCode}.");
            }

            TEnvelope? envelope;

            try
            {
                envelope = await response.Content.ReadFromJsonAsync<TEnvelope>(JsonOptions, ct);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Could not parse the Prozorro response for {Kind} {Id}", kind, id);
                return Error.Failure("Prozorro.InvalidResponse", "The Prozorro API response could not be read.");
            }

            var data = envelope is null ? null : selector(envelope);

            if (data is null)
                return Error.Failure("Prozorro.InvalidResponse", "The Prozorro API response contains no data.");

            return data;
        }
    }
}
