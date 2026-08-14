using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>Cursor pagination state; follow <see cref="NextCursor"/> until <see cref="HasMore"/> is false.</summary>
public sealed class Pagination
{
    /// <summary>Maximum number of records requested per page.</summary>
    [JsonPropertyName("limit")] public int Limit { get; set; }
    /// <summary>True when at least one more page is available.</summary>
    [JsonPropertyName("hasMore")] public bool HasMore { get; set; }
    /// <summary>Opaque cursor for the next page; pass back as <c>cursor</c>. Null when <see cref="HasMore"/> is false.</summary>
    [JsonPropertyName("nextCursor")] public string? NextCursor { get; set; }
}
