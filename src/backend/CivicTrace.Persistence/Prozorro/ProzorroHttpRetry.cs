using System.Net;

namespace CivicTrace.Persistence.Prozorro;

internal static class ProzorroHttpRetry
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromMinutes(1);

    public static async Task<HttpResponseMessage> GetAsync(
        HttpClient client,
        string path,
        CancellationToken ct)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await client.GetAsync(path, ct);
            }
            catch (HttpRequestException) when (attempt < MaxAttempts)
            {
                await Task.Delay(Backoff(attempt), ct);
                continue;
            }

            if (attempt == MaxAttempts || !IsTransient(response.StatusCode))
                return response;

            var delay = GetRetryDelay(response, attempt);
            if (delay is null)
                return response;

            response.Dispose();
            await Task.Delay(delay.Value, ct);
        }

        throw new InvalidOperationException("The Prozorro retry loop exited unexpectedly.");
    }

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode is HttpStatusCode.RequestTimeout
            or HttpStatusCode.TooManyRequests
            or HttpStatusCode.InternalServerError
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout;

    private static TimeSpan? GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter?.Delta;
        if (retryAfter is null && response.Headers.RetryAfter?.Date is { } date)
            retryAfter = date - DateTimeOffset.UtcNow;

        if (retryAfter > MaxRetryAfter)
            return null;

        return retryAfter is { } value && value > TimeSpan.Zero
            ? value
            : Backoff(attempt);
    }

    private static TimeSpan Backoff(int attempt)
        => TimeSpan.FromMilliseconds(400 * Math.Pow(2, attempt - 1));
}
