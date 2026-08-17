using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using TeTS.Integrations.Http;
using TeTS.Integrations.Models;

namespace TeTS.Integrations.Resources;

/// <summary>Training catalog export: map course identifiers for SSO deep links and completions interpretation.</summary>
public sealed class CatalogResource
{
    private const string BasePath = "/api/integrations/v1/catalog";
    private readonly ApiConnection _connection;
    internal CatalogResource(ApiConnection connection) => _connection = connection;

    /// <summary>
    /// Streams the resolved organization's training pool, following pagination automatically.
    /// Argument validation (page size range) runs eagerly on call — before any iteration or HTTP
    /// request — rather than being deferred to the first <c>MoveNextAsync</c>, matching the other
    /// resources' fail-fast contract.
    /// </summary>
    /// <remarks>
    /// <see cref="CatalogItem.LegacyCourseId"/> is the id the completions report emits as
    /// <c>courseId</c> and SSO accepts as <c>courseId</c>/<c>cid</c>; programs deep-link via the
    /// SSO <c>programId</c> parameter using <see cref="CatalogItem.LegacyProgramId"/>. Rows with
    /// <see cref="CatalogItem.RenewOnly"/> true are superseded editions kept for interpreting
    /// historical completions — do not deep-link them for new assignments.
    /// </remarks>
    /// <param name="options">Optional page size and tenant override; see <see cref="ListCatalogOptions"/>.</param>
    /// <param name="cancellationToken">Token to cancel enumeration.</param>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ListCatalogOptions.PageSize"/> is outside 1..1000.</exception>
    /// <exception cref="TetsApiException">The server returned an error response, or pagination stalled (see <see cref="TetsErrorCode.PaginationStalled"/>).</exception>
    public IAsyncEnumerable<CatalogItem> ListAsync(ListCatalogOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ValidateListOptions(options);
        return EnumerateItemsAsync(options, cancellationToken);
    }

    private async IAsyncEnumerable<CatalogItem> EnumerateItemsAsync(ListCatalogOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? cursor = null;
        while (true)
        {
            var query = new List<KeyValuePair<string, string>>();
            if (options?.PageSize is int pageSize)
                query.Add(new("limit", pageSize.ToString(CultureInfo.InvariantCulture)));
            if (cursor is not null) query.Add(new("cursor", cursor));

            var page = await _connection.SendAsync<CatalogListResponse>(HttpMethod.Get, BasePath, query,
                tenantOverride: options?.OrganizationTenantId, ct: cancellationToken).ConfigureAwait(false);
            foreach (var item in page.Items) yield return item;
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

    private static void ValidateListOptions(ListCatalogOptions? options)
    {
        if (options?.PageSize is int pageSize && (pageSize < 1 || pageSize > 1000))
            throw new ArgumentOutOfRangeException(nameof(options), pageSize,
                "ListCatalogOptions.PageSize must be between 1 and 1000.");
    }
}
