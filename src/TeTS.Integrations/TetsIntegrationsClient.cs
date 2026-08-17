using System.Net.Http;
using TeTS.Integrations.Http;
using TeTS.Integrations.Models;
using TeTS.Integrations.Resources;
using TeTS.Integrations.Sso;

namespace TeTS.Integrations;

/// <summary>
/// Entry point for the TeTS Integrations API v1. Create once and reuse for the lifetime of your
/// application (or DI scope) rather than constructing a new instance per call.
/// </summary>
/// <remarks>
/// <see cref="Dispose"/> only disposes the underlying <see cref="HttpClient"/> when this instance
/// created it (the options-only constructor). When you inject your own <see cref="HttpClient"/>
/// (DI / <c>IHttpClientFactory</c>), disposing this client instance leaves that injected
/// <see cref="HttpClient"/> untouched — you own its lifetime.
/// </remarks>
public sealed class TetsIntegrationsClient : IDisposable
{
    private readonly ApiConnection _connection;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _baseUrl;
    private readonly string? _integrationSlug;
    private readonly string? _ssoSecret;
    private SsoUrlBuilder? _sso;

    /// <summary>Creates a client that owns its own HttpClient; disposing this instance disposes it too.</summary>
    public TetsIntegrationsClient(TetsOptions options)
        : this(CreateOwnedClient(options), options, ownsHttpClient: true) { }

    /// <summary>Creates a client over an injected HttpClient (DI / IHttpClientFactory). The SDK does not
    /// change its Timeout, and disposing this client does not dispose the injected HttpClient.</summary>
    public TetsIntegrationsClient(HttpClient httpClient, TetsOptions options)
        : this(httpClient, options, ownsHttpClient: false) { }

    private TetsIntegrationsClient(HttpClient httpClient, TetsOptions options, bool ownsHttpClient)
    {
        ValidateOptions(options);
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        _baseUrl = options.BaseUrl;
        _integrationSlug = options.IntegrationSlug;
        _ssoSecret = options.SsoSecret;
        _connection = new ApiConnection(httpClient, options, info => LastRateLimit = info);
        Users = new UsersResource(_connection);
        Reports = new ReportsResource(_connection);
        Catalog = new CatalogResource(_connection);
    }

    /// <summary>Validates before allocating, so invalid options never leave an undisposed HttpClient behind.</summary>
    private static HttpClient CreateOwnedClient(TetsOptions options)
    {
        ValidateOptions(options);
        return new HttpClient { Timeout = options.Timeout };
    }

    /// <summary>
    /// Rate-limit state from the most recent API response, when the server sent it. Updated after
    /// every response; under concurrent requests this is last-writer-wins and only approximate.
    /// </summary>
    public RateLimitInfo? LastRateLimit { get; private set; }

    /// <summary>
    /// Onboarding smoke check: verifies the API key and returns the resolved connection/tenant. After
    /// retries are exhausted, a transport-level failure (no response ever received) surfaces as
    /// <see cref="HttpRequestException"/> or <see cref="TaskCanceledException"/> — not
    /// <see cref="TetsApiException"/>, which is reserved for responses the server actually returned.
    /// </summary>
    public Task<PingResponse> PingAsync(string? organizationTenantId = null, CancellationToken cancellationToken = default)
        => _connection.SendAsync<PingResponse>(HttpMethod.Get, "/api/integrations/v1/ping",
            tenantOverride: organizationTenantId, ct: cancellationToken);

    /// <summary>User provisioning and lifecycle operations.</summary>
    public UsersResource Users { get; }

    /// <summary>Completion reporting.</summary>
    public ReportsResource Reports { get; }

    /// <summary>Training catalog export.</summary>
    public CatalogResource Catalog { get; }

    /// <summary>
    /// Signed SSO launch URL builder. Requires <see cref="TetsOptions.IntegrationSlug"/> and
    /// <see cref="TetsOptions.SsoSecret"/>; throws <see cref="InvalidOperationException"/> otherwise.
    /// </summary>
    public SsoUrlBuilder Sso
    {
        get
        {
            if (_sso is not null) return _sso;
            if (string.IsNullOrWhiteSpace(_integrationSlug) || string.IsNullOrWhiteSpace(_ssoSecret))
                throw new InvalidOperationException(
                    "Set TetsOptions.IntegrationSlug and TetsOptions.SsoSecret to build SSO launch URLs.");
            return _sso = new SsoUrlBuilder(_baseUrl, _integrationSlug!, _ssoSecret!);
        }
    }

    /// <summary>Disposes the underlying HttpClient, but only if this instance created it — see remarks.</summary>
    public void Dispose()
    {
        if (_ownsHttpClient) _httpClient.Dispose();
    }

    private static TetsOptions ValidateOptions(TetsOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.BaseUrl))
            throw new ArgumentException("TetsOptions.BaseUrl is required.", nameof(options));
        if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("TetsOptions.BaseUrl must be an absolute http or https URL.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new ArgumentException("TetsOptions.ApiKey is required.", nameof(options));
        if (options.Timeout <= TimeSpan.Zero)
            throw new ArgumentException("TetsOptions.Timeout must be greater than zero.", nameof(options));
        return options;
    }
}
