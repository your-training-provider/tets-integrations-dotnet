using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>
/// Result of <see cref="TeTS.Integrations.Resources.UsersResource.SetHireDatesAsync"/>. A row that does
/// not match a learner, or carries an out-of-range date, is skipped and reported; it never fails the batch.
/// </summary>
public sealed class SetHireDatesResult
{
    /// <summary>Rows received.</summary>
    [JsonPropertyName("total")] public int Total { get; set; }
    /// <summary>Rows whose learner's hire date was set.</summary>
    [JsonPropertyName("updated")] public int Updated { get; set; }
    /// <summary>Rows skipped; see each row's <see cref="HireDateRowResult.Reason"/>.</summary>
    [JsonPropertyName("skipped")] public int Skipped { get; set; }
    /// <summary>One result per row sent, in request order.</summary>
    [JsonPropertyName("results")] public IReadOnlyList<HireDateRowResult> Results { get; set; } = Array.Empty<HireDateRowResult>();
}

/// <summary>The outcome for one row of a <c>Users.SetHireDatesAsync</c> call.</summary>
public sealed class HireDateRowResult
{
    /// <summary>The row's external identifier, as sent; null when the row had none.</summary>
    [JsonPropertyName("externalId")] public string? ExternalId { get; set; }
    /// <summary>The row's email, as sent; null when the row had none.</summary>
    [JsonPropertyName("email")] public string? Email { get; set; }
    /// <summary><c>updated</c> or <c>skipped</c>.</summary>
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    /// <summary>How the learner was matched: <c>external_id</c> or <c>email</c>. Null when no learner was matched.</summary>
    [JsonPropertyName("matchedBy")] public string? MatchedBy { get; set; }
    /// <summary>Why the row was skipped, in words. Null when it was not skipped.</summary>
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    /// <summary>Platform user ID of the matched learner. Null when no learner was matched.</summary>
    [JsonPropertyName("userId")] public string? UserId { get; set; }
}

/// <summary>Wire shape of the hire-dates request body. Internal.</summary>
internal sealed class SetHireDatesRequest
{
    [JsonPropertyName("employees")] public IReadOnlyList<HireDateUpdate> Employees { get; set; } = Array.Empty<HireDateUpdate>();
}
