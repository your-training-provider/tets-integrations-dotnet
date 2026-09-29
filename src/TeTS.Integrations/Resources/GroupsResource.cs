using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using TeTS.Integrations.Http;
using TeTS.Integrations.Models;

namespace TeTS.Integrations.Resources;

/// <summary>Group directory: discover the group ids user provisioning and the roster filter take.</summary>
public sealed class GroupsResource
{
    private const string BasePath = "/api/integrations/v1/groups";
    private readonly ApiConnection _connection;
    internal GroupsResource(ApiConnection connection) => _connection = connection;

    /// <summary>
    /// Streams every group in the resolved organization, following pagination automatically.
    /// Argument validation (page size range) runs eagerly on call — before any iteration or HTTP
    /// request — rather than being deferred to the first <c>MoveNextAsync</c>, matching the other
    /// resources' fail-fast contract.
    /// </summary>
    /// <remarks>
    /// Groups a customer adds later appear on the next call, so list on a schedule rather than
    /// storing ids you were sent by hand. Match the group ids you already hold through
    /// <see cref="GroupItem.LegacyGroupId"/>, which is the same in every environment. The
    /// organization root is listed with <see cref="GroupItem.AcceptsMembers"/> false: it cannot
    /// receive members, so do not send its id in <c>GroupIds</c>.
    /// </remarks>
    /// <param name="options">Optional page size and tenant override; see <see cref="ListGroupsOptions"/>.</param>
    /// <param name="cancellationToken">Token to cancel enumeration.</param>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ListGroupsOptions.PageSize"/> is outside 1..1000.</exception>
    /// <exception cref="TetsApiException">The server returned an error response, or pagination stalled (see <see cref="TetsErrorCode.PaginationStalled"/>).</exception>
    public IAsyncEnumerable<GroupItem> ListAsync(ListGroupsOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ValidateListOptions(options);
        return EnumerateItemsAsync(options, cancellationToken);
    }

    private async IAsyncEnumerable<GroupItem> EnumerateItemsAsync(ListGroupsOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? cursor = null;
        while (true)
        {
            var query = new List<KeyValuePair<string, string>>();
            if (options?.PageSize is int pageSize)
                query.Add(new("limit", pageSize.ToString(CultureInfo.InvariantCulture)));
            if (cursor is not null) query.Add(new("cursor", cursor));

            var page = await _connection.SendAsync<GroupListResponse>(HttpMethod.Get, BasePath, query,
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

    private static void ValidateListOptions(ListGroupsOptions? options)
    {
        if (options?.PageSize is int pageSize && (pageSize < 1 || pageSize > 1000))
            throw new ArgumentOutOfRangeException(nameof(options), pageSize,
                "ListGroupsOptions.PageSize must be between 1 and 1000.");
    }
}
