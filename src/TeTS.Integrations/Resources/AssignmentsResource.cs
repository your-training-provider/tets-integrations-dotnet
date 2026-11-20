using System.Net;
using System.Net.Http;
using TeTS.Integrations.Http;
using TeTS.Integrations.Models;

namespace TeTS.Integrations.Resources;

/// <summary>
/// Training assignment: assign a course or program to a learner, and read where each assignment
/// stands. Needs a key with the <c>assignments:write</c> / <c>assignments:read</c> scopes, which only
/// an organization's own API access keys carry (Settings &gt; API access); other keys get
/// <see cref="TetsErrorCode.InsufficientScope"/>.
/// </summary>
public sealed class AssignmentsResource
{
    private const string BasePath = "/api/integrations/v1/assignments";
    private readonly ApiConnection _connection;
    internal AssignmentsResource(ApiConnection connection) => _connection = connection;

    /// <summary>
    /// Assigns one course or program to one learner, under the organization's seats, the way a manager
    /// assigns it in the app. A program is one assignment (its courses are tracked inside it); a new
    /// assignment's window is 365 days. No email is sent to the learner.
    /// </summary>
    /// <remarks>
    /// Safe to repeat: when the learner already holds an active assignment for the title, nothing is
    /// written and the result carries that assignment with <see cref="CreateAssignmentResult.Created"/>
    /// false. That also makes the SDK's automatic retries on transient failures safe, so no
    /// Idempotency-Key is needed. The server checks, in order: the organization's Basic subscription
    /// is active (<see cref="TetsErrorCode.OrgBasicSubscriptionLapsed"/>), the learner is yours
    /// (<see cref="TetsErrorCode.IntegrationUserNotFound"/>, <see cref="TetsErrorCode.IntegrationUserOutOfScope"/>),
    /// the learner is active (<see cref="TetsErrorCode.IntegrationBadInput"/> when deactivated; reactivate
    /// with <c>Users.ActivateAsync</c>), the organization carries the title
    /// (<see cref="TetsErrorCode.ProductNotAvailable"/>, also for an unknown id and a <c>RenewOnly</c>
    /// edition), and a seat is left this license year (<see cref="TetsErrorCode.SeatsExhausted"/>).
    /// Server limit: 60 calls a minute per key.
    /// </remarks>
    /// <param name="request">The title (<c>ProductId</c>) and exactly one of <c>ExternalId</c> / <c>UserId</c>.</param>
    /// <param name="organizationTenantId">Overrides <see cref="TetsOptions.OrganizationTenantId"/> for this call only.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException"><c>ProductId</c> is blank, or <c>ExternalId</c> / <c>UserId</c> are not exactly one set.</exception>
    /// <exception cref="TetsApiException">The server refused the assignment (see remarks), or the response could not be unwrapped.</exception>
    public async Task<CreateAssignmentResult> AssignAsync(CreateAssignmentRequest request,
        string? organizationTenantId = null, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.ProductId))
            throw new ArgumentException("ProductId is required.", nameof(request));
        var hasExternalId = !string.IsNullOrWhiteSpace(request.ExternalId);
        var hasUserId = !string.IsNullOrWhiteSpace(request.UserId);
        if (hasExternalId == hasUserId)
            throw new ArgumentException("Set exactly one of ExternalId or UserId to identify the learner.", nameof(request));

        var result = await _connection.SendAsync<CreateAssignmentResult>(HttpMethod.Post, BasePath,
            body: request, tenantOverride: organizationTenantId, ct: cancellationToken).ConfigureAwait(false);
        if (result.Assignment is null)
            throw new TetsApiException(HttpStatusCode.OK, TetsErrorCode.Unknown,
                "SDK check failed: the server response did not contain the expected 'assignment' object.",
                requestId: null, details: null, rawBody: null);
        return result;
    }

    /// <summary>
    /// Lists a learner's assignments in your organization, newest first, each with the status the app
    /// shows for it. Writes nothing.
    /// </summary>
    /// <remarks>
    /// Lists your organization's assignments plus the learner's assignments that carry no organization
    /// (their own purchase, a partner launch); another organization's are never listed. Server limit:
    /// 30 calls a minute per key.
    /// </remarks>
    /// <param name="externalId">Your stable staff identifier for the learner. Required.</param>
    /// <param name="productId">Only this title's assignments (a <see cref="CatalogItem.ProductId"/>). Optional.</param>
    /// <param name="organizationTenantId">Overrides <see cref="TetsOptions.OrganizationTenantId"/> for this call only.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <exception cref="ArgumentException"><paramref name="externalId"/> is null, empty, or whitespace, or <paramref name="productId"/> is set but blank.</exception>
    /// <exception cref="TetsApiException">The learner is not yours (<see cref="TetsErrorCode.IntegrationUserNotFound"/>), or the server returned another error.</exception>
    public Task<IReadOnlyList<Assignment>> GetByExternalIdAsync(string externalId, string? productId = null,
        string? organizationTenantId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("externalId is required.", nameof(externalId));
        ValidateProductFilter(productId);
        return ListAsync("externalId", externalId, productId, organizationTenantId, cancellationToken);
    }

    /// <summary>
    /// Lists a learner's assignments by platform user ID; otherwise identical to
    /// <see cref="GetByExternalIdAsync"/>.
    /// </summary>
    /// <param name="userId">Platform user ID of the learner. Required.</param>
    /// <param name="productId">Only this title's assignments (a <see cref="CatalogItem.ProductId"/>). Optional.</param>
    /// <param name="organizationTenantId">Overrides <see cref="TetsOptions.OrganizationTenantId"/> for this call only.</param>
    /// <param name="cancellationToken">Token to cancel the request.</param>
    /// <exception cref="ArgumentException"><paramref name="userId"/> is null, empty, or whitespace, or <paramref name="productId"/> is set but blank.</exception>
    /// <exception cref="TetsApiException">The learner is not yours (<see cref="TetsErrorCode.IntegrationUserNotFound"/>), or the server returned another error.</exception>
    public Task<IReadOnlyList<Assignment>> GetByUserIdAsync(string userId, string? productId = null,
        string? organizationTenantId = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId is required.", nameof(userId));
        ValidateProductFilter(productId);
        return ListAsync("userId", userId, productId, organizationTenantId, cancellationToken);
    }

    private async Task<IReadOnlyList<Assignment>> ListAsync(string identifierName, string identifier,
        string? productId, string? organizationTenantId, CancellationToken cancellationToken)
    {
        var query = new List<KeyValuePair<string, string>> { new(identifierName, identifier) };
        if (productId is not null) query.Add(new("productId", productId));

        var response = await _connection.SendAsync<AssignmentListResponse>(HttpMethod.Get, BasePath, query,
            tenantOverride: organizationTenantId, ct: cancellationToken).ConfigureAwait(false);
        if (response.Assignments is null)
            throw new TetsApiException(HttpStatusCode.OK, TetsErrorCode.Unknown,
                "SDK check failed: the server response did not contain the expected 'assignments' array.",
                requestId: null, details: null, rawBody: null);
        return response.Assignments;
    }

    private static void ValidateProductFilter(string? productId)
    {
        if (productId is not null && string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("productId must not be empty or whitespace when set.", nameof(productId));
    }
}
