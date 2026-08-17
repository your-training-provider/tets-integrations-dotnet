using System.Net;
using TeTS.Integrations.Models;

namespace TeTS.Integrations;

/// <summary>
/// Thrown when the Integrations API returns an error response. Carries the stable
/// error <see cref="Code"/> and the <see cref="RequestId"/> to include in support requests.
/// </summary>
public sealed class TetsApiException : Exception
{
    /// <summary>The HTTP status code of the response that produced this exception.</summary>
    public HttpStatusCode StatusCode { get; }
    /// <summary>The stable, machine-readable error code — see the README's error-code table for handling guidance.</summary>
    public TetsErrorCode Code { get; }
    /// <summary>Server-generated request ID — include this in any support request to TeTS.</summary>
    public string? RequestId { get; }
    /// <summary>Field-level validation problems, if the server supplied any; empty otherwise.</summary>
    public IReadOnlyList<ErrorDetail> Details { get; }
    /// <summary>The raw response body, for debugging. Bodies larger than 64 KiB are truncated,
    /// ending with <c>...[truncated by SDK]</c>.</summary>
    public string? RawBody { get; }

    /// <summary>
    /// Constructs the exception. Used both for server-returned error responses and for
    /// SDK-synthesized failures (e.g. an unparseable success body or a stalled pagination cursor).
    /// </summary>
    /// <param name="statusCode">The response's HTTP status code.</param>
    /// <param name="code">The mapped stable error code.</param>
    /// <param name="message">Human-readable description; the HTTP status (and requestId, if present) is appended automatically.</param>
    /// <param name="requestId">Server-generated request ID, or null when unavailable.</param>
    /// <param name="details">Field-level validation problems, or null when the server didn't supply any.</param>
    /// <param name="rawBody">The raw response body, for debugging, or null when unavailable.</param>
    /// <param name="innerException">The underlying exception (e.g. a JSON parse failure), if any.</param>
    public TetsApiException(HttpStatusCode statusCode, TetsErrorCode code, string message,
        string? requestId, IReadOnlyList<ErrorDetail>? details, string? rawBody, Exception? innerException = null)
        : base(requestId is null ? $"{message} (HTTP {(int)statusCode})"
                                 : $"{message} (HTTP {(int)statusCode}, requestId: {requestId})", innerException)
    {
        StatusCode = statusCode;
        Code = code;
        RequestId = requestId;
        Details = details ?? Array.Empty<ErrorDetail>();
        RawBody = rawBody;
    }
}
