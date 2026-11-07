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

    public async Task<ErrorOr<ProzorroTender>> GetTenderAsync(string apiId, CancellationToken ct = default)
    {
        HttpResponseMessage response;

        try
        {
            response = await httpClient.GetAsync($"tenders/{apiId}", ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Prozorro API request failed for tender {ApiId}", apiId);
            return Error.Failure("Prozorro.Unreachable", "The Prozorro API is unreachable. Try again later.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Error.Failure("Prozorro.Timeout", "The Prozorro API did not respond in time. Try again later.");
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.NotFound)
                return Error.NotFound("Prozorro.TenderNotFound", $"Tender '{apiId}' was not found in the Prozorro API.");

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return Error.Failure("Prozorro.RateLimited", "The Prozorro API is rate limiting requests. Try again in a minute.");

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Prozorro API returned {StatusCode} for tender {ApiId}", (int)response.StatusCode, apiId);

                return Error.Failure(
                    "Prozorro.Unavailable",
                    $"The Prozorro API returned status {(int)response.StatusCode}.");
            }

            ProzorroTenderEnvelope? envelope;

            try
            {
                envelope = await response.Content.ReadFromJsonAsync<ProzorroTenderEnvelope>(JsonOptions, ct);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Could not parse the Prozorro response for tender {ApiId}", apiId);
                return Error.Failure("Prozorro.InvalidResponse", "The Prozorro API response could not be read.");
            }

            if (envelope?.Data is null)
                return Error.Failure("Prozorro.InvalidResponse", "The Prozorro API response contains no data.");

            return envelope.Data;
        }
    }
}