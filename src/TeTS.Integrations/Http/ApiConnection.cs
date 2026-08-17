using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using TeTS.Integrations.Models;

namespace TeTS.Integrations.Http;

/// <summary>Internal transport: URL building, auth/tenant/idempotency headers, retries, error mapping.</summary>
internal sealed class ApiConnection
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string? _tenantId;
    private readonly int _maxRetries;
    private readonly Action<RateLimitInfo> _onRateLimit;
    private static readonly Random Jitter = new();
    private static readonly string UserAgent = BuildUserAgent();

    public ApiConnection(HttpClient http, TetsOptions options, Action<RateLimitInfo> onRateLimit)
    {
        _http = http;
        // Snapshot the values this connection needs instead of holding a live TetsOptions
        // reference, so post-construction mutation of the caller's options object can't
        // change transport behavior mid-flight.
        _apiKey = options.ApiKey;
        _baseUrl = options.BaseUrl.TrimEnd('/');
        _tenantId = options.OrganizationTenantId;
        _maxRetries = options.MaxRetries;
        _onRateLimit = onRateLimit;
    }

    /// <summary>
    /// Sends a request and deserializes the response. Retries per <c>TetsOptions.MaxRetries</c> on
    /// 429/5xx responses, idempotency-in-flight conflicts, and transport failures. Once retries are
    /// exhausted, a transport-level failure (no response ever received) surfaces as
    /// <see cref="HttpRequestException"/> or <see cref="TaskCanceledException"/> — not
    /// <see cref="TetsApiException"/>, which is reserved for responses the server actually returned.
    /// </summary>
    public async Task<T> SendAsync<T>(HttpMethod method, string path,
        IReadOnlyList<KeyValuePair<string, string>>? query = null, object? body = null,
        string? idempotencyKey = null, string? tenantOverride = null, CancellationToken ct = default)
    {
        var url = BuildUrl(path, query);
        var maxAttempts = Math.Max(0, _maxRetries) + 1;

        for (var attempt = 1; ; attempt++)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            var tenant = tenantOverride ?? _tenantId;
            if (!string.IsNullOrWhiteSpace(tenant))
                request.Headers.TryAddWithoutValidation("X-Integration-Tenant-Id", tenant);
            if (idempotencyKey is not null)
                request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
            if (body is not null)
                request.Content = new StringContent(
                    JsonSerializer.Serialize(body, body.GetType(), TetsJson.Options),
                    Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, ct).ConfigureAwait(false);
            }
            catch (HttpRequestException) when (attempt < maxAttempts)
            {
                await BackoffAsync(attempt, retryAfter: null, ct).ConfigureAwait(false);
                continue;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested && attempt < maxAttempts)
            {
                // HttpClient timeout (not a caller cancellation)
                await BackoffAsync(attempt, retryAfter: null, ct).ConfigureAwait(false);
                continue;
            }

            using (response)
            {
                CaptureRateLimit(response);
                var raw = response.Content is null
                    ? ""
                    : await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    if (string.IsNullOrWhiteSpace(raw))
                        throw new TetsApiException(response.StatusCode, TetsErrorCode.Unknown,
                            "Empty response body.", HeaderRequestId(response), null, TruncateRawBody(raw));

                    try
                    {
                        return JsonSerializer.Deserialize<T>(raw, TetsJson.Options)
                               ?? throw new TetsApiException(response.StatusCode, TetsErrorCode.Unknown,
                                    "Empty response body.", HeaderRequestId(response), null, TruncateRawBody(raw));
                    }
                    catch (JsonException jx)
                    {
                        throw new TetsApiException(response.StatusCode, TetsErrorCode.Unknown,
                            "Response body was not valid JSON.", HeaderRequestId(response), null, TruncateRawBody(raw), jx);
                    }
                }

                ErrorEnvelope? envelope = null;
                try { envelope = JsonSerializer.Deserialize<ErrorEnvelope>(raw, TetsJson.Options); }
                catch (JsonException) { /* non-JSON error body (proxy/gateway) */ }

                var code = TetsErrorCodeMapper.Map(envelope?.Code);
                if (code == TetsErrorCode.Unknown && (int)response.StatusCode == 429)
                    code = TetsErrorCode.RateLimited;

                if (attempt < maxAttempts && ShouldRetry(response.StatusCode, code))
                {
                    await BackoffAsync(attempt, RetryAfterDelay(response), ct).ConfigureAwait(false);
                    continue;
                }

                throw new TetsApiException(response.StatusCode, code,
                    envelope?.Error ?? $"Request failed with HTTP {(int)response.StatusCode}.",
                    envelope?.RequestId ?? HeaderRequestId(response), envelope?.Details, TruncateRawBody(raw));
            }
        }
    }

    private string BuildUrl(string path, IReadOnlyList<KeyValuePair<string, string>>? query)
    {
        if (query is null || query.Count == 0) return _baseUrl + path;
        var sb = new StringBuilder(_baseUrl).Append(path).Append('?');
        for (var i = 0; i < query.Count; i++)
        {
            if (i > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(query[i].Key)).Append('=').Append(Uri.EscapeDataString(query[i].Value));
        }
        return sb.ToString();
    }

    private static bool ShouldRetry(HttpStatusCode status, TetsErrorCode code)
        => (int)status == 429 || (int)status >= 500
           || (status == HttpStatusCode.Conflict && code == TetsErrorCode.IdempotencyRequestInFlight);

    private static TimeSpan? RetryAfterDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null) return null;
        var delta = retryAfter.Delta;
        if (delta is null && retryAfter.Date is DateTimeOffset date)
            delta = date - DateTimeOffset.UtcNow;
        return delta is null ? null : ClampRetryAfter(delta.Value);
    }

    /// <summary>
    /// Clamps a server-supplied Retry-After delay to [0, 60] seconds, so a misconfigured or
    /// malicious server cannot stall a caller for an unbounded time nor drive a fast retry loop
    /// via a negative value.
    /// </summary>
    internal static TimeSpan ClampRetryAfter(TimeSpan delta)
    {
        var seconds = Math.Max(0, Math.Min(60, delta.TotalSeconds));
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Caps what <see cref="TetsApiException.RawBody"/> retains at 64 KiB, so a huge error body
    /// isn't kept alive for the lifetime of the exception (logs, exception trackers). Parsing
    /// always runs on the full body; only the stored copy is truncated.
    /// </summary>
    internal static string TruncateRawBody(string raw)
    {
        const int maxChars = 64 * 1024;
        return raw.Length <= maxChars ? raw : raw.Substring(0, maxChars) + "\n...[truncated by SDK]";
    }

    private static string? HeaderRequestId(HttpResponseMessage response)
        => response.Headers.TryGetValues("X-Request-Id", out var values) ? values.FirstOrDefault() : null;

    private static async Task BackoffAsync(int attempt, TimeSpan? retryAfter, CancellationToken ct)
    {
        double seconds;
        if (retryAfter is TimeSpan ra) seconds = ra.TotalSeconds;
        else
        {
            seconds = Math.Min(0.5 * Math.Pow(2, attempt - 1), 8);
            lock (Jitter) seconds += Jitter.NextDouble() * 0.25;
        }
        await Task.Delay(TimeSpan.FromSeconds(seconds), ct).ConfigureAwait(false);
    }

    private void CaptureRateLimit(HttpResponseMessage response)
    {
        int? Header(string name) => response.Headers.TryGetValues(name, out var v)
            && int.TryParse(v.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
        long? LongHeader(string name) => response.Headers.TryGetValues(name, out var v)
            && long.TryParse(v.FirstOrDefault(), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : null;
        var limit = Header("X-RateLimit-Limit");
        var remaining = Header("X-RateLimit-Remaining");
        var reset = LongHeader("X-RateLimit-Reset");
        if (limit is not null || remaining is not null || reset is not null)
            _onRateLimit(new RateLimitInfo { Limit = limit, Remaining = remaining, ResetEpochSeconds = reset });
    }

    private static string BuildUserAgent()
    {
        var version = typeof(TetsIntegrationsClient).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return $"TeTS.Integrations/{(string.IsNullOrWhiteSpace(version) ? "1.0.0-beta.1" : version)}";
    }
}
