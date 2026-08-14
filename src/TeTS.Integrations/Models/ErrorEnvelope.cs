using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>One field-level validation problem inside an API error.</summary>
public sealed class ErrorDetail
{
    /// <summary>Dot-path of the offending request field, e.g. <c>email</c> or <c>groupIds.0</c>.</summary>
    [JsonPropertyName("field")] public string Field { get; set; } = "";
    /// <summary>Human-readable description of what is wrong with <see cref="Field"/>.</summary>
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

/// <summary>Wire shape of the API error envelope. Internal; surfaced via <see cref="TetsApiException"/>.</summary>
internal sealed class ErrorEnvelope
{
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("requestId")] public string? RequestId { get; set; }
    [JsonPropertyName("details")] public List<ErrorDetail>? Details { get; set; }
}

/// <summary>Wire wrapper for endpoints that return <c>{ "user": ... }</c>.</summary>
internal sealed class UserEnvelope<T>
{
    [System.Text.Json.Serialization.JsonPropertyName("user")] public T? User { get; set; }
}
