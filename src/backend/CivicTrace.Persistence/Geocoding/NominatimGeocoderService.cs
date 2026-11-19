using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using CivicTrace.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace CivicTrace.Persistence.Geocoding;

public sealed partial class NominatimGeocoderService(
    HttpClient httpClient,
    ILogger<NominatimGeocoderService> logger) : IGeocoderService
{
    private static readonly ConcurrentDictionary<string, (double Lat, double Lng)?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly SemaphoreSlim RateLock = new(1, 1);
    private static DateTimeOffset _lastRequestAt = DateTimeOffset.MinValue;

    public async Task<(double Latitude, double Longitude)?> GeocodePoltavaAsync(string address, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        var normalized = NormalizeForSearch(address);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (Cache.TryGetValue(normalized, out var cached))
            return cached;

        await RateLock.WaitAsync(ct);
        try
        {
            if (Cache.TryGetValue(normalized, out cached))
                return cached;

            var elapsed = DateTimeOffset.UtcNow - _lastRequestAt;
            if (elapsed < TimeSpan.FromMilliseconds(1100))
                await Task.Delay(TimeSpan.FromMilliseconds(1100) - elapsed, ct);

            _lastRequestAt = DateTimeOffset.UtcNow;

            var url =
                $"search?format=json&limit=1&countrycodes=ua&viewbox=34.43,49.66,34.68,49.51&bounded=1&q={Uri.EscapeDataString(normalized)}";

            var hits = await httpClient.GetFromJsonAsync<List<NominatimHit>>(url, ct);
            var hit = hits?.FirstOrDefault();

            if (hit is not null &&
                double.TryParse(hit.Lat, CultureInfo.InvariantCulture, out var lat) &&
                double.TryParse(hit.Lon, CultureInfo.InvariantCulture, out var lon))
            {
                var coords = (lat, lon);
                Cache[normalized] = coords;
                return coords;
            }

            Cache[normalized] = null;
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Nominatim geocoding failed for '{Address}'", normalized);
            return null;
        }
        finally
        {
            RateLock.Release();
        }
    }

    private static string NormalizeForSearch(string address)
    {
        var text = address.Trim();
        text = PostalCodeRegex().Replace(text, "");
        text = StreetPrefixRegex().Replace(text, "");
        if (!text.Contains("Полтав", StringComparison.OrdinalIgnoreCase))
            text = $"Полтава, {text}";
        return text.Trim(' ', ',');
    }

    [GeneratedRegex(@"\b\d{5}\b,?\s*")]
    private static partial Regex PostalCodeRegex();

    [GeneratedRegex(@"\b(м\.|місто|вул\.|вулиця|пров\.|буд\.|будинок|просп\.|пл\.)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex StreetPrefixRegex();

    private sealed class NominatimHit
    {
        public string? Lat { get; init; }
        public string? Lon { get; init; }
    }
}