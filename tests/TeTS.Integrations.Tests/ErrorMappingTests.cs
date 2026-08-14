using System.Net;
using TeTS.Integrations;
using TeTS.Integrations.Models;
using Xunit;

namespace TeTS.Integrations.Tests;

public class ErrorMappingTests
{
    [Theory]
    [InlineData("VALIDATION_ERROR", TetsErrorCode.ValidationError)]
    [InlineData("INTEGRATION_CONNECTION_REQUIRED", TetsErrorCode.IntegrationConnectionRequired)]
    [InlineData("INTEGRATION_USER_IDENTIFIER_REQUIRED", TetsErrorCode.IntegrationUserIdentifierRequired)]
    [InlineData("INTEGRATION_BAD_INPUT", TetsErrorCode.IntegrationBadInput)]
    [InlineData("UNAUTHORIZED", TetsErrorCode.Unauthorized)]
    [InlineData("INTEGRATION_CONNECTION_FORBIDDEN", TetsErrorCode.IntegrationConnectionForbidden)]
    [InlineData("INTEGRATION_CONNECTION_INACTIVE", TetsErrorCode.IntegrationConnectionInactive)]
    [InlineData("INTEGRATION_USER_OUT_OF_SCOPE", TetsErrorCode.IntegrationUserOutOfScope)]
    [InlineData("INSUFFICIENT_SCOPE", TetsErrorCode.InsufficientScope)]
    [InlineData("INTEGRATION_USER_NOT_FOUND", TetsErrorCode.IntegrationUserNotFound)]
    [InlineData("IDEMPOTENCY_REQUEST_IN_FLIGHT", TetsErrorCode.IdempotencyRequestInFlight)]
    [InlineData("INTEGRATION_EXTERNAL_ID_TAKEN", TetsErrorCode.IntegrationExternalIdTaken)]
    [InlineData("USER_EMAIL_TAKEN", TetsErrorCode.UserEmailTaken)]
    [InlineData("USERNAME_TAKEN", TetsErrorCode.UsernameTaken)]
    [InlineData("IDEMPOTENCY_KEY_REUSED", TetsErrorCode.IdempotencyKeyReused)]
    [InlineData("RATE_LIMITED", TetsErrorCode.RateLimited)]
    [InlineData("INTERNAL_ERROR", TetsErrorCode.InternalError)]
    [InlineData("FEATURE_DISABLED", TetsErrorCode.FeatureDisabled)]
    public void MapsEveryStableCode(string wire, TetsErrorCode expected)
        => Assert.Equal(expected, TetsErrorCodeMapper.Map(wire));

    [Theory]
    [InlineData("SOME_FUTURE_CODE")]
    [InlineData(null)]
    [InlineData("")]
    public void UnknownCodesNeverThrow(string? wire)
        => Assert.Equal(TetsErrorCode.Unknown, TetsErrorCodeMapper.Map(wire));

    [Fact]
    public void ExceptionCarriesEnvelopeFields()
    {
        var details = new[] { new ErrorDetail { Field = "email", Message = "invalid" } };
        var ex = new TetsApiException(HttpStatusCode.BadRequest, TetsErrorCode.ValidationError,
            "Validation failed.", "req_123", details, "{raw}");
        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(TetsErrorCode.ValidationError, ex.Code);
        Assert.Equal("req_123", ex.RequestId);
        Assert.Single(ex.Details);
        Assert.Equal("{raw}", ex.RawBody);
        Assert.Contains("Validation failed.", ex.Message);
        Assert.Contains("req_123", ex.Message); // requestId in message = support-packet ergonomics
    }
}
