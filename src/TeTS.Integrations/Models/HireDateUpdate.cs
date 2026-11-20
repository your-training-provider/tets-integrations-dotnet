using System.Text.Json.Serialization;
using TeTS.Integrations.Http;

namespace TeTS.Integrations.Models;

/// <summary>
/// One row for <see cref="TeTS.Integrations.Resources.UsersResource.SetHireDatesAsync"/>: a learner's
/// hire date, matched by <see cref="ExternalId"/> or <see cref="Email"/> (set at least one).
/// </summary>
public sealed class HireDateUpdate
{
    /// <summary>Your stable staff identifier for the learner. Set this, <see cref="Email"/>, or both.</summary>
    [JsonPropertyName("externalId")] public string? ExternalId { get; set; }
    /// <summary>The learner's email address, used to match when <see cref="ExternalId"/> is not set or not found.</summary>
    [JsonPropertyName("email")] public string? Email { get; set; }
    /// <summary>
    /// The hire date. Only the calendar date is sent (<c>yyyy-MM-dd</c>, no timezone conversion). The
    /// server accepts dates from 1950 up to 60 days in the future (UTC days); a row outside that range is
    /// skipped and reported, not failed.
    /// </summary>
    [JsonPropertyName("hireDate")]
    [JsonConverter(typeof(CalendarDateJsonConverter))]
    public DateTime HireDate { get; set; }
}
