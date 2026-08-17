using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using TeTS.Integrations.Http;
using TeTS.Integrations.Models;

namespace TeTS.Integrations.Resources;

/// <summary>User provisioning and lifecycle: create, look up, list, update, activate/deactivate.</summary>
public sealed class UsersResource
{
    private const string BasePath = "/api/integrations/v1/users";
    private readonly ApiConnection _connection;
    internal UsersResource(ApiConnection connection) => _connection = connection;

    /// <summary>
    /// Creates a platform user linked to your integration. Retries are safe: the SDK sends an
    /// Idempotency-Key (auto-generated unless <paramref name="idempotencyKey"/> is provided) and
    /// reuses it across its internal retries.
    /// </summary>
    /// <param name="request">The user to create.</param>
    /// <param name="idempotencyKey">Optional caller-supplied Idempotency-Key; auto-generated when omitted.</param>
    /// <param name="organizationTenantId">Overrides <see cref="TetsOptions.OrganizationTenantId"/> for this call only.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="TetsApiException">The server rejected the request, or its response could not be unwrapped.</exception>
    public async Task<CreateUserResult> CreateAsync(CreateUserRequest request, string? idempotencyKey = null,
        string? organizationTenantId = null, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        var envelope = await _connection.SendAsync<UserEnvelope<CreateUserResult>>(HttpMethod.Post, BasePath,
            body: request, idempotencyKey: idempotencyKey ?? $"tets-sdk-{Guid.NewGuid():N}",
            tenantOverride: organizationTenantId, ct: cancellationToken).ConfigureAwait(false);
        return Unwrap(envelope);
    }

    /// <summary>Fetches a user by your stable external identifier.</summary>
    /// <param name="externalId">Your stable staff identifier for the user. Required.</param>
    /// <param name="organizationTenantId">Overrides <see cref="TetsOptions.OrganizationTenantId"/> for this call only.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <exception cref="ArgumentException"><paramref name="externalId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="TetsApiException">No matching user was found, or the response could not be unwrapped.</exception>
    public async Task<User> GetByExternalIdAsync(string externalId, string? organizationTenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("externalId is required.", nameof(externalId));

        var envelope = await _connection.SendAsync<UserEnvelope<User>>(HttpMethod.Get, BasePath,
            query: new[] { new KeyValuePair<string, string>("externalId", externalId) },
            tenantOverride: organizationTenantId, ct: cancellationToken).ConfigureAwait(false);
        return Unwrap(envelope);
    }

    /// <summary>
    /// Streams the roster of users in the resolved organization, following pagination automatically.
    /// Argument validation (page size range, group id shape) runs eagerly on call — before any
    /// iteration or HTTP request — rather than being deferred to the first <c>MoveNextAsync</c>,
    /// matching the other resources' fail-fast contract.
    /// </summary>
    /// <remarks>
    /// <see cref="UserListItem.ExternalId"/> is null for users not yet linked to your integration
    /// (for example accounts migrated from the legacy platform).
    /// </remarks>
    /// <param name="options">Optional group filter, page size, and tenant override; see <see cref="ListUsersOptions"/>.</param>
    /// <param name="cancellationToken">Token to cancel enumeration.</param>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="ListUsersOptions.PageSize"/> is outside 1..1000.</exception>
    /// <exception cref="ArgumentException"><see cref="ListUsersOptions.GroupId"/> is set but empty or whitespace.</exception>
    /// <exception cref="TetsApiException">The server returned an error response, or pagination stalled (see <see cref="TetsErrorCode.PaginationStalled"/>).</exception>
    public IAsyncEnumerable<UserListItem> ListAsync(ListUsersOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ValidateListOptions(options);
        return EnumerateUsersAsync(options, cancellationToken);
    }

    private async IAsyncEnumerable<UserListItem> EnumerateUsersAsync(ListUsersOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? cursor = null;
        while (true)
        {
            var query = new List<KeyValuePair<string, string>>();
            if (options?.PageSize is int pageSize)
                query.Add(new("limit", pageSize.ToString(CultureInfo.InvariantCulture)));
            if (cursor is not null) query.Add(new("cursor", cursor));
            if (options?.GroupId is not null) query.Add(new("groupId", options.GroupId));

            var page = await _connection.SendAsync<UserListResponse>(HttpMethod.Get, BasePath + "/list", query,
                tenantOverride: options?.OrganizationTenantId, ct: cancellationToken).ConfigureAwait(false);
            foreach (var user in page.Users) yield return user;
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

    private static void ValidateListOptions(ListUsersOptions? options)
    {
        if (options?.PageSize is int pageSize && (pageSize < 1 || pageSize > 1000))
            throw new ArgumentOutOfRangeException(nameof(options), pageSize,
                "ListUsersOptions.PageSize must be between 1 and 1000.");
        if (options?.GroupId is { } groupId && string.IsNullOrWhiteSpace(groupId))
            throw new ArgumentException("ListUsersOptions.GroupId must not be empty or whitespace when set.", nameof(options));
    }

    /// <summary>Partial profile update; only fields set on <paramref name="request"/> are changed.</summary>
    /// <param name="request">The fields to change. Set <see cref="UpdateUserRequest.ExternalId"/> or <see cref="UpdateUserRequest.UserId"/> to identify the user.</param>
    /// <param name="organizationTenantId">Overrides <see cref="TetsOptions.OrganizationTenantId"/> for this call only.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">Neither <see cref="UpdateUserRequest.ExternalId"/> nor <see cref="UpdateUserRequest.UserId"/> is set.</exception>
    /// <exception cref="TetsApiException">No matching user was found, or the response could not be unwrapped.</exception>
    public async Task<User> UpdateAsync(UpdateUserRequest request, string? organizationTenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.ExternalId) && string.IsNullOrWhiteSpace(request.UserId))
            throw new ArgumentException("Set ExternalId or UserId to identify the user.", nameof(request));

        var envelope = await _connection.SendAsync<UserEnvelope<User>>(new HttpMethod("PATCH"), BasePath,
            body: request, tenantOverride: organizationTenantId, ct: cancellationToken).ConfigureAwait(false);
        return Unwrap(envelope);
    }

    /// <summary>Checks username availability and whether it is linked to your integration.</summary>
    /// <param name="userName">The candidate username to check. Required.</param>
    /// <param name="organizationTenantId">Overrides <see cref="TetsOptions.OrganizationTenantId"/> for this call only.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <exception cref="ArgumentException"><paramref name="userName"/> is null, empty, or whitespace.</exception>
    /// <exception cref="TetsApiException">The server returned an error response.</exception>
    public Task<UserExistsResponse> CheckExistsAsync(string userName, string? organizationTenantId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("userName is required.", nameof(userName));

        return _connection.SendAsync<UserExistsResponse>(HttpMethod.Get, BasePath + "/exists",
            query: new[] { new KeyValuePair<string, string>("userName", userName) },
            tenantOverride: organizationTenantId, ct: cancellationToken);
    }

    /// <summary>Sets a user's status to active.</summary>
    /// <param name="externalId">Your stable staff identifier for the user. Required.</param>
    /// <param name="organizationTenantId">Overrides <see cref="TetsOptions.OrganizationTenantId"/> for this call only.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <exception cref="ArgumentException"><paramref name="externalId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="TetsApiException">No matching user was found, or the response could not be unwrapped.</exception>
    public Task<UserStatusResult> ActivateAsync(string externalId, string? organizationTenantId = null,
        CancellationToken cancellationToken = default)
        => SetStatusAsync(externalId, active: true, organizationTenantId, cancellationToken);

    /// <summary>Sets a user's status to inactive (deactivates their access).</summary>
    /// <param name="externalId">Your stable staff identifier for the user. Required.</param>
    /// <param name="organizationTenantId">Overrides <see cref="TetsOptions.OrganizationTenantId"/> for this call only.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <exception cref="ArgumentException"><paramref name="externalId"/> is null, empty, or whitespace.</exception>
    /// <exception cref="TetsApiException">No matching user was found, or the response could not be unwrapped.</exception>
    public Task<UserStatusResult> DeactivateAsync(string externalId, string? organizationTenantId = null,
        CancellationToken cancellationToken = default)
        => SetStatusAsync(externalId, active: false, organizationTenantId, cancellationToken);

    private async Task<UserStatusResult> SetStatusAsync(string externalId, bool active,
        string? organizationTenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("externalId is required.", nameof(externalId));

        var envelope = await _connection.SendAsync<UserEnvelope<UserStatusResult>>(
            new HttpMethod("PATCH"), BasePath + "/status",
            body: new UserStatusChangeRequest { ExternalId = externalId, Status = active ? "active" : "inactive" },
            tenantOverride: organizationTenantId, ct: cancellationToken).ConfigureAwait(false);
        return Unwrap(envelope);
    }

    /// <summary>
    /// Unwraps the <c>user</c> field of an envelope, failing fast with a <see cref="TetsApiException"/>
    /// (rather than returning null or letting a caller hit a distant <see cref="NullReferenceException"/>)
    /// when the server returns a 2xx body without the expected object, e.g. <c>{}</c> or <c>{"user":null}</c>.
    /// </summary>
    private static T Unwrap<T>(UserEnvelope<T> envelope) where T : class
        => envelope.User ?? throw new TetsApiException(HttpStatusCode.OK, TetsErrorCode.Unknown,
            "SDK check failed: the server response did not contain the expected 'user' object.", requestId: null, details: null, rawBody: null);
}
