namespace TeTS.Integrations;

/// <summary>Stable machine-readable error codes returned by the Integrations API v1 contract.</summary>
public enum TetsErrorCode
{
    /// <summary>The server returned a code this SDK version does not know (forward compatibility).</summary>
    Unknown = 0,
    /// <summary>Request body or query failed validation.</summary>
    ValidationError,
    /// <summary>No active connection exists for this integration/organization.</summary>
    IntegrationConnectionRequired,
    /// <summary>The request needs <c>externalId</c> or <c>userId</c> to identify a user.</summary>
    IntegrationUserIdentifierRequired,
    /// <summary>Integration-specific input was malformed.</summary>
    IntegrationBadInput,
    /// <summary>The API key is missing, invalid, or expired.</summary>
    Unauthorized,
    /// <summary>Your connection is not allowed to perform this operation.</summary>
    IntegrationConnectionForbidden,
    /// <summary>Your connection has been deactivated.</summary>
    IntegrationConnectionInactive,
    /// <summary>The user is outside your integration's scope (wrong org/connection).</summary>
    IntegrationUserOutOfScope,
    /// <summary>Your API key lacks the scope this endpoint requires.</summary>
    InsufficientScope,
    /// <summary>No user found for this integration matching the identifier given.</summary>
    IntegrationUserNotFound,
    /// <summary>Another request with the same Idempotency-Key is still being processed.</summary>
    IdempotencyRequestInFlight,
    /// <summary><c>externalId</c> is already linked to a different user in this organization.</summary>
    IntegrationExternalIdTaken,
    /// <summary>The email is already in use by another user.</summary>
    UserEmailTaken,
    /// <summary>The username is already in use.</summary>
    UsernameTaken,
    /// <summary>The same Idempotency-Key was sent with a different request body than the original.</summary>
    IdempotencyKeyReused,
    /// <summary>You've exceeded the rate limit for this key/route; see <see cref="RateLimitInfo"/> and <c>Retry-After</c>.</summary>
    RateLimited,
    /// <summary>Unexpected server error; safe to retry.</summary>
    InternalError,
    /// <summary>The Integrations API is disabled on this environment.</summary>
    FeatureDisabled,
    /// <summary>
    /// Client-side: pagination did not advance (the SDK aborted a stalled cursor loop); never sent
    /// by the server.
    /// </summary>
    PaginationStalled,
}

/// <summary>Maps wire error-code strings to <see cref="TetsErrorCode"/>.</summary>
public static class TetsErrorCodeMapper
{
    private static readonly Dictionary<string, TetsErrorCode> Wire = new(StringComparer.Ordinal)
    {
        ["VALIDATION_ERROR"] = TetsErrorCode.ValidationError,
        ["INTEGRATION_CONNECTION_REQUIRED"] = TetsErrorCode.IntegrationConnectionRequired,
        ["INTEGRATION_USER_IDENTIFIER_REQUIRED"] = TetsErrorCode.IntegrationUserIdentifierRequired,
        ["INTEGRATION_BAD_INPUT"] = TetsErrorCode.IntegrationBadInput,
        ["UNAUTHORIZED"] = TetsErrorCode.Unauthorized,
        ["INTEGRATION_CONNECTION_FORBIDDEN"] = TetsErrorCode.IntegrationConnectionForbidden,
        ["INTEGRATION_CONNECTION_INACTIVE"] = TetsErrorCode.IntegrationConnectionInactive,
        ["INTEGRATION_USER_OUT_OF_SCOPE"] = TetsErrorCode.IntegrationUserOutOfScope,
        ["INSUFFICIENT_SCOPE"] = TetsErrorCode.InsufficientScope,
        ["INTEGRATION_USER_NOT_FOUND"] = TetsErrorCode.IntegrationUserNotFound,
        ["IDEMPOTENCY_REQUEST_IN_FLIGHT"] = TetsErrorCode.IdempotencyRequestInFlight,
        ["INTEGRATION_EXTERNAL_ID_TAKEN"] = TetsErrorCode.IntegrationExternalIdTaken,
        ["USER_EMAIL_TAKEN"] = TetsErrorCode.UserEmailTaken,
        ["USERNAME_TAKEN"] = TetsErrorCode.UsernameTaken,
        ["IDEMPOTENCY_KEY_REUSED"] = TetsErrorCode.IdempotencyKeyReused,
        ["RATE_LIMITED"] = TetsErrorCode.RateLimited,
        ["INTERNAL_ERROR"] = TetsErrorCode.InternalError,
        ["FEATURE_DISABLED"] = TetsErrorCode.FeatureDisabled,
    };

    /// <summary>Returns the enum for a wire code; <see cref="TetsErrorCode.Unknown"/> for null/unrecognized.</summary>
    public static TetsErrorCode Map(string? wireCode)
        => wireCode is not null && Wire.TryGetValue(wireCode, out var code) ? code : TetsErrorCode.Unknown;
}
