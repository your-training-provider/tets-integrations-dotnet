using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TeTS.Integrations.Sso;

/// <summary>Builds signed SSO launch URLs (legacy Topyx-compatible MD5 signature scheme).</summary>
public sealed class SsoUrlBuilder
{
    private readonly string _baseUrl;
    private readonly string _integrationSlug;
    private readonly string _secret;

    /// <summary>Constructs a builder bound to one integration's base URL, slug, and SSO secret.</summary>
    /// <param name="baseUrl">Absolute https origin of the platform, no path/query/fragment, e.g. https://courses.example.com. http is accepted only for loopback hosts (localhost, 127.0.0.1, ::1) during local development.</param>
    /// <param name="integrationSlug">Your integration's slug, sent as the <c>integration</c> query parameter.</param>
    /// <param name="ssoSecret">Your integration's SSO signing secret. ASCII only; never echoed in exception messages.</param>
    /// <exception cref="ArgumentException"><paramref name="baseUrl"/> is missing, not an absolute https (or loopback http) URL, or contains a query/fragment; or <paramref name="integrationSlug"/> or <paramref name="ssoSecret"/> is missing or non-ASCII.</exception>
    public SsoUrlBuilder(string baseUrl, string integrationSlug, string ssoSecret)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("baseUrl is required.", nameof(baseUrl));
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("baseUrl must be an absolute http or https URL.", nameof(baseUrl));
        // http would send the signed launch URL (and the learner's session) in cleartext; allow it
        // only for loopback hosts (localhost, 127.0.0.1, ::1) so local development still works.
        if (baseUri.Scheme == Uri.UriSchemeHttp && !baseUri.IsLoopback)
            throw new ArgumentException(
                "baseUrl must use https; http is allowed only for localhost development.", nameof(baseUrl));
        if (!string.IsNullOrEmpty(baseUri.Query) || !string.IsNullOrEmpty(baseUri.Fragment))
            throw new ArgumentException("baseUrl must not contain a query string or fragment.", nameof(baseUrl));
        if (string.IsNullOrWhiteSpace(integrationSlug)) throw new ArgumentException("integrationSlug is required.", nameof(integrationSlug));
        if (string.IsNullOrWhiteSpace(ssoSecret)) throw new ArgumentException("ssoSecret is required.", nameof(ssoSecret));
        // Never echo the offending character for the secret — it would leak secret material into
        // partner logs and exception trackers.
        foreach (var ch in ssoSecret)
            if (ch > 0x7F)
                throw new ArgumentException("ssoSecret contains non-ASCII characters.", nameof(ssoSecret));
        _baseUrl = baseUrl.TrimEnd('/');
        _integrationSlug = integrationSlug;
        _secret = ssoSecret;
    }

    /// <summary>
    /// MD5(secret + username + sessionTimeOut + timestamp), lowercase hex — byte-identical to the
    /// server implementation. All inputs must be ASCII; non-ASCII throws (the server's legacy
    /// encoding is not representable portably, so we fail loudly instead of signing wrongly). The
    /// <paramref name="secret"/>'s non-ASCII characters are never echoed in the exception message,
    /// to avoid leaking secret material into partner logs; other inputs may safely name the
    /// offending field and character.
    /// </summary>
    /// <remarks>
    /// Requires the legacy MD5 algorithm for server-compatible signing; on FIPS-enforced
    /// Windows/.NET Framework hosts, MD5 may be unavailable and this throws
    /// <see cref="InvalidOperationException"/> — see the SDK README's FIPS section. The server also
    /// enforces a ~5-minute (±300 s) max age on the signed timestamp, so build and use launch URLs
    /// at click time rather than caching or batching them.
    /// </remarks>
    public static string ComputeSignature(string secret, string username, string sessionTimeOutSeconds, string timestamp)
        => ComputeSignatureCore(secret, username, sessionTimeOutSeconds, timestamp, MD5.Create);

    /// <summary>
    /// Test seam for <see cref="ComputeSignature"/>: takes an injectable MD5 factory so FIPS-unavailable
    /// behavior can be exercised without a real FIPS-enforced host. Not part of the public API.
    /// </summary>
    internal static string ComputeSignatureCore(
        string secret, string username, string sessionTimeOutSeconds, string timestamp, Func<MD5> md5Factory)
    {
        RequireAscii(secret, "secret", echoChar: false);
        RequireAscii(username, "username", echoChar: true);
        RequireAscii(sessionTimeOutSeconds, "sessionTimeOutSeconds", echoChar: true);
        RequireAscii(timestamp, "timestamp", echoChar: true);

        var payload = secret + username + sessionTimeOutSeconds + timestamp;

        MD5 md5;
        try
        {
            md5 = md5Factory();
        }
        catch (Exception ex) when (ex is InvalidOperationException or CryptographicException)
        {
            throw new InvalidOperationException(
                "The TeTS SSO scheme requires legacy MD5 for server compatibility, which is unavailable " +
                "on this host (FIPS-enforced mode?). On FIPS-enforced Windows hosts, see the SDK README's " +
                "FIPS section.", ex);
        }

        using (md5)
        {
            var hash = md5.ComputeHash(Encoding.ASCII.GetBytes(payload));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private static void RequireAscii(string value, string fieldName, bool echoChar)
    {
        foreach (var ch in value)
        {
            if (ch <= 0x7F) continue;
            throw echoChar
                ? new ArgumentException(
                    $"{fieldName} contains non-ASCII character '{ch}'. Ask TeTS support about non-ASCII usernames.")
                : new ArgumentException($"{fieldName} contains non-ASCII characters.");
        }
    }

    /// <summary>Builds the fully signed launch URL; redirect the learner's browser to it.</summary>
    /// <remarks>
    /// Interpolate the result with <see cref="Uri.AbsoluteUri"/>, not the bare <c>ToString()</c> —
    /// <c>Uri.ToString()</c> un-escapes reserved characters and can hand the learner's browser a
    /// malformed URL. The server enforces a ~5-minute (±300 s) max age on the signed timestamp, so
    /// build this URL at click time rather than caching or batching it for later use. On
    /// FIPS-enforced Windows/.NET Framework hosts, MD5 (required for server-compatible signing) may
    /// be unavailable — see the SDK README's FIPS section.
    /// </remarks>
    /// <param name="request">The launch parameters. <see cref="SsoLaunchRequest.UserName"/> is required.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="request"/> fails validation (e.g. missing UserName, or an invalid EmbedOrigin shape).</exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="SsoLaunchRequest.SessionTimeOutSeconds"/> or <see cref="SsoLaunchRequest.TimestampOverride"/> is out of range.</exception>
    public Uri BuildLaunchUrl(SsoLaunchRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.UserName))
            throw new ArgumentException("UserName is required.", nameof(request));
        if (request.SessionTimeOutSeconds < 1 || request.SessionTimeOutSeconds > 28800)
            throw new ArgumentOutOfRangeException(nameof(request),
                "SessionTimeOutSeconds must be 1..28800 (8 hours max).");
        if (request.TimestampOverride is <= 0)
            throw new ArgumentOutOfRangeException(nameof(request),
                "TimestampOverride must be a positive unix-seconds value when provided.");
        if (request.Embed && string.IsNullOrWhiteSpace(request.EmbedOrigin))
            throw new ArgumentException("EmbedOrigin is required when Embed is true.", nameof(request));
        if (!request.Embed && !string.IsNullOrWhiteSpace(request.EmbedOrigin))
            throw new ArgumentException("Set Embed = true when providing EmbedOrigin.", nameof(request));
        if (!string.IsNullOrWhiteSpace(request.EmbedOrigin))
            // The netstandard2.0 reference assembly's string.IsNullOrWhiteSpace lacks a
            // NotNullWhen(false) annotation, so the compiler can't narrow the null-check above
            // on that TFM; the guard makes this provably non-null.
            ValidateEmbedOriginShape(request.EmbedOrigin!, nameof(request));

        // The server trims every query value before verifying the signature — sign and emit the
        // same trimmed value so a stray leading/trailing space can never cause a mismatch.
        var username = request.UserName.Trim();
        var timestamp = (request.TimestampOverride ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            .ToString(CultureInfo.InvariantCulture);
        var timeout = request.SessionTimeOutSeconds.ToString(CultureInfo.InvariantCulture);
        var signature = ComputeSignature(_secret, username, timeout, timestamp);

        var pairs = new List<KeyValuePair<string, string?>>
        {
            new("integration", _integrationSlug),
            new("username", username),
            new("timestamp", timestamp),
            new("sessionTimeOut", timeout),
            new("signature", signature),
            new("identification", request.Identification),
            new("firstName", request.FirstName),
            new("lastName", request.LastName),
            new("email", request.Email),
            new("organization", request.Organization),
            new("jobTitle", request.JobTitle),
            new("courseId", request.CourseId),
            new("courseName", request.CourseName),
            new("contentId", request.ContentId),
            new("programId", request.ProgramId),
            new("programName", request.ProgramName),
            new("organizationTenantId", request.OrganizationTenantId),
            new("embed", request.Embed ? "1" : null),
            new("embedOrigin", request.EmbedOrigin),
        };

        var sb = new StringBuilder(_baseUrl).Append("/api/integrations/v1/sso?");
        var first = true;
        foreach (var pair in pairs)
        {
            if (string.IsNullOrEmpty(pair.Value)) continue;
            if (!first) sb.Append('&');
            first = false;
            sb.Append(Uri.EscapeDataString(pair.Key)).Append('=').Append(Uri.EscapeDataString(pair.Value));
        }
        return new Uri(sb.ToString());
    }

    private static void ValidateEmbedOriginShape(string origin, string paramName)
    {
        const string shapeHint = "EmbedOrigin must be an origin only, e.g. https://app.partner.example " +
            "(https required; http allowed only for localhost/127.0.0.1; no userinfo, no path beyond \"/\", " +
            "no query, no fragment).";

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var embedUri))
            throw new ArgumentException($"{shapeHint} Got: '{origin}'.", paramName);

        var isLoopback = embedUri.Host is "localhost" or "127.0.0.1";
        var schemeOk = embedUri.Scheme == Uri.UriSchemeHttps || (embedUri.Scheme == Uri.UriSchemeHttp && isLoopback);
        if (!schemeOk
            || embedUri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(embedUri.UserInfo)
            || !string.IsNullOrEmpty(embedUri.Query)
            || !string.IsNullOrEmpty(embedUri.Fragment))
            throw new ArgumentException($"{shapeHint} Got: '{origin}'.", paramName);
    }
}
