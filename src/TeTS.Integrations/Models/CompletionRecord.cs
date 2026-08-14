using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>One completed training for a user linked to your integration.</summary>
public sealed class CompletionRecord
{
    /// <summary>Platform username of the learner, when available.</summary>
    [JsonPropertyName("userName")] public string? UserName { get; set; }
    /// <summary>The learner's first name.</summary>
    [JsonPropertyName("firstName")] public string FirstName { get; set; } = "";
    /// <summary>The learner's last name.</summary>
    [JsonPropertyName("lastName")] public string LastName { get; set; } = "";
    /// <summary>Display name of the completed course.</summary>
    [JsonPropertyName("courseName")] public string CourseName { get; set; } = "";
    /// <summary>Legacy numeric course id when available.</summary>
    [JsonPropertyName("courseId")] public int? CourseId { get; set; }
    /// <summary>Platform user ID of the learner.</summary>
    [JsonPropertyName("userId")] public string UserId { get; set; } = "";
    /// <summary>Final numeric score/mark, when the course records one.</summary>
    [JsonPropertyName("finalMark")] public double? FinalMark { get; set; }
    /// <summary>Your stable staff identifier for the learner, when linked to your integration.</summary>
    [JsonPropertyName("externalId")] public string? ExternalId { get; set; }
    /// <summary>Alternate identification number for the learner, when configured.</summary>
    [JsonPropertyName("identificationNumber")] public string? IdentificationNumber { get; set; }
    /// <summary>The learner's organization/company name, when recorded.</summary>
    [JsonPropertyName("organization")] public string? Organization { get; set; }
    /// <summary>The learner's country, when recorded.</summary>
    [JsonPropertyName("country")] public string? Country { get; set; }
    /// <summary>Timestamp the completion was recorded.</summary>
    [JsonPropertyName("completedDate")] public DateTimeOffset CompletedDate { get; set; }
    /// <summary>Timestamp the learner was registered for the course, when available.</summary>
    [JsonPropertyName("dateRegistered")] public DateTimeOffset? DateRegistered { get; set; }
    /// <summary>The learner's email address, when recorded.</summary>
    [JsonPropertyName("email")] public string? Email { get; set; }
    /// <summary>Product SKU.</summary>
    [JsonPropertyName("code")] public string? Code { get; set; }
    /// <summary>When the completion/certification expires, for courses with a renewal cycle.</summary>
    [JsonPropertyName("expiresAt")] public DateTimeOffset? ExpiresAt { get; set; }
}
