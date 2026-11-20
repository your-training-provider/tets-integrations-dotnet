using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>Result of <see cref="TeTS.Integrations.Resources.AssignmentsResource.AssignAsync"/>.</summary>
public sealed class CreateAssignmentResult
{
    /// <summary>The assignment: the one this call wrote, or the active one the learner already held.</summary>
    [JsonPropertyName("assignment")] public Assignment Assignment { get; set; } = null!;
    /// <summary>
    /// True when this call wrote the assignment (HTTP 201); false when the learner already held an
    /// active assignment for the title and nothing was written (HTTP 200). Repeating a call is safe.
    /// </summary>
    [JsonPropertyName("created")] public bool Created { get; set; }
}
