using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>One page of the completions report.</summary>
public sealed class CompletionsReport
{
    /// <summary>Start of the requested date range (inclusive), echoed back from the request.</summary>
    [JsonPropertyName("from")] public DateTimeOffset From { get; set; }
    /// <summary>End of the requested date range (inclusive), echoed back from the request.</summary>
    [JsonPropertyName("to")] public DateTimeOffset To { get; set; }
    /// <summary>Number of completions in this page.</summary>
    [JsonPropertyName("count")] public int Count { get; set; }
    /// <summary>The completions in this page.</summary>
    [JsonPropertyName("completions")] public List<CompletionRecord> Completions { get; set; } = new();
    /// <summary>Cursor state for fetching subsequent pages.</summary>
    [JsonPropertyName("pagination")] public Pagination Pagination { get; set; } = new();
}
