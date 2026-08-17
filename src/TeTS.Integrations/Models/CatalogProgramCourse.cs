using System.Text.Json.Serialization;

namespace TeTS.Integrations.Models;

/// <summary>One child course of a program row returned by <c>Catalog.ListAsync</c>.</summary>
public sealed class CatalogProgramCourse
{
    /// <summary>Product id of the child course. The course also appears as its own catalog row.</summary>
    [JsonPropertyName("productId")] public string ProductId { get; set; } = "";
    /// <summary>Position of the course within the program.</summary>
    [JsonPropertyName("sortOrder")] public int SortOrder { get; set; }
    /// <summary>True when the course is required to complete the program; false for elective pool members.</summary>
    [JsonPropertyName("isRequired")] public bool IsRequired { get; set; }
}
