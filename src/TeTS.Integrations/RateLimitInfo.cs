namespace TeTS.Integrations;

/// <summary>Rate-limit state from the most recent API response (per key, per route on the server).</summary>
public sealed class RateLimitInfo
{
    /// <summary>The total request quota for the current window, from <c>X-RateLimit-Limit</c>.</summary>
    public int? Limit { get; init; }
    /// <summary>Requests remaining in the current window, from <c>X-RateLimit-Remaining</c>.</summary>
    public int? Remaining { get; init; }
    /// <summary>Epoch seconds when the current window resets.</summary>
    public long? ResetEpochSeconds { get; init; }
}
