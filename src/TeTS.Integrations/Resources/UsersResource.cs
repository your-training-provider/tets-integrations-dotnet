using System.Net;
using System.Net.Http;
using TeTS.Integrations.Http;
using TeTS.Integrations.Models;

namespace TeTS.Integrations.Resources;

/// <summary>User provisioning and lifecycle: create, look up, update, activate/deactivate.</summary>
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
