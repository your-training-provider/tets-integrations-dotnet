using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace TeTS.Integrations.Http;

/// <summary>Shared, read-only serializer settings: explicit wire names, omit nulls (PATCH partial semantics).</summary>
public static class TetsJson
{
    /// <summary>
    /// The shared <see cref="JsonSerializerOptions"/> instance used for every SDK request/response body.
    /// Locked via <see cref="JsonSerializerOptions.MakeReadOnly()"/> at construction — do not mutate;
    /// any attempt to change a setting on this instance throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // MakeReadOnly() requires an explicit resolver; this keeps the existing
            // reflection-based (de)serialization behavior while allowing the lock below.
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        // Locks the instance so no consumer can mutate settings shared across every request.
        options.MakeReadOnly();
        return options;
    }
}
