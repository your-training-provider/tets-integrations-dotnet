using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using TeTS.Integrations.Http;
using TeTS.Integrations.Models;

namespace TeTS.Integrations.Resources;

/// <summary>Completion reporting for users linked to your integration.</summary>
public sealed class ReportsResource
{
    private const string Path = "/api/integrations/v1/reports/completions";
    private readonly ApiConnection _connection;
    internal ReportsResource(ApiConnection connection) => _connection = connection;

    /// <summary>Fetches one page of completions in [from, to] (dates inclusive, sent as yyyy-MM-dd).</summary>
    /// <remarks>
    /// Only the calendar date (Year/Month/Day) of <paramref name="from"/> and <paramref name="to"/> is
    /// used: each is sent verbatim as <c>yyyy-MM-dd</c> with no timezone conversion, and
    /// <see cref="DateTime.Kind"/> is ignored. Callers mixing <see cref="DateTime.Now"/> and
    /// <see cref="DateTime.UtcNow"/> near midnight may end up querying a different calendar day than
    /// intended. Range endpoints are inclusive, evaluated by the server.
    /// </remarks>
    /// <param name="from">Start of the date range (inclusive); only the calendar date is used.</param>
    /// <param name="to">End of the date range (inclusive); only the calendar date is used. Must not be earlier than <paramref name="from"/>.</param>
    /// <param name="cursor">Opaque cursor from a previous page's <c>Pagination.NextCursor</c>; omit for the first page.</param>
    /// <param name="limit">Page size, 1..1000. Server default applies when omitted.</param>
    /// <param name="organizationTenantId">Overrides <see cref="TetsOptions.OrganizationTenantId"/> for this call only.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <exception cref="ArgumentException"><paramref name="to"/> is earlier than <paramref name="from"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1..1000.</exception>
    /// <exception cref="TetsApiException">The server returned an error response.</exception>
    public Task<CompletionsReport> GetCompletionsPageAsync(DateTime from, DateTime to,
        string? cursor = null, int? limit = null, string? organizationTenantId = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRange(from, to, limit);

        var query = new List<KeyValuePair<string, string>>
        {
            new("from", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new("to", to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
        };
        if (limit is int l) query.Add(new("limit", l.ToString(CultureInfo.InvariantCulture)));
        if (cursor is not null) query.Add(new("cursor", cursor));
        return _connection.SendAsync<CompletionsReport>(HttpMethod.Get, Path, query,
            tenantOverride: organizationTenantId, ct: cancellationToken);
    }

    /// <summary>
    /// Streams every completion in [from, to], following pagination automatically. Argument validation
    /// (date order, limit range) runs eagerly on call — before any iteration or HTTP request — rather
    /// than being deferred to the first <c>MoveNextAsync</c>, matching the other resources' fail-fast
    /// contract.
    /// </summary>
    /// <remarks>
    /// Only the calendar date (Year/Month/Day) of <paramref name="from"/> and <paramref name="to"/> is
    /// used: each is sent verbatim as <c>yyyy-MM-dd</c> with no timezone conversion, and
    /// <see cref="DateTime.Kind"/> is ignored. Callers mixing <see cref="DateTime.Now"/> and
    /// <see cref="DateTime.UtcNow"/> near midnight may end up querying a different calendar day than
    /// intended. Range endpoints are inclusive, evaluated by the server.
    /// </remarks>
    /// <param name="from">Start of the date range (inclusive); only the calendar date is used.</param>
    /// <param name="to">End of the date range (inclusive); only the calendar date is used. Must not be earlier than <paramref name="from"/>.</param>
    /// <param name="limit">Page size per underlying request, 1..1000. Server default applies when omitted.</param>
    /// <param name="organizationTenantId">Overrides <see cref="TetsOptions.OrganizationTenantId"/> for this call only.</param>
    /// <param name="cancellationToken">Token to cancel enumeration.</param>
    /// <exception cref="ArgumentException"><paramref name="to"/> is earlier than <paramref name="from"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="limit"/> is outside 1..1000.</exception>
    /// <exception cref="TetsApiException">The server returned an error response, or pagination stalled (see <see cref="TetsErrorCode.PaginationStalled"/>).</exception>
    public IAsyncEnumerable<CompletionRecord> GetCompletionsAsync(DateTime from, DateTime to,
        int? limit = null, string? organizationTenantId = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRange(from, to, limit);
        return EnumerateCompletionsAsync(from, to, limit, organizationTenantId, cancellationToken);
    }

    private async IAsyncEnumerable<CompletionRecord> EnumerateCompletionsAsync(DateTime from, DateTime to,
        int? limit, string? organizationTenantId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? cursor = null;
        while (true)
        {
            var page = await GetCompletionsPageAsync(from, to, cursor, limit, organizationTenantId, cancellationToken)
                .ConfigureAwait(false);
            foreach (var record in page.Completions) yield return record;
            if (!page.Pagination.HasMore || page.Pagination.NextCursor is null) yield break;

            // Self-DoS guard: a server that reports hasMore=true but echoes back the same cursor we
            // just used would otherwise drive this loop into an infinite request cycle. Fail loudly
            // instead of hammering the API forever.
            if (string.Equals(page.Pagination.NextCursor, cursor, StringComparison.Ordinal))
                throw new TetsApiException(HttpStatusCode.OK, TetsErrorCode.PaginationStalled,
                    "SDK check failed: pagination did not advance; the server returned the same cursor twice. Aborting to avoid an infinite request loop.",
                    requestId: null, details: null, rawBody: null);

            cursor = page.Pagination.NextCursor;
        }
    }

    private static void ValidateRange(DateTime from, DateTime to, int? limit)
    {
        if (to < from)
            throw new ArgumentException("to must not be earlier than from.", nameof(to));
        if (limit is int l && (l < 1 || l > 1000))
            throw new ArgumentOutOfRangeException(nameof(limit), l, "limit must be between 1 and 1000.");
    }
}
