using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TeTS.Integrations.Http;

/// <summary>
/// (De)serializes a <see cref="DateTime"/> as a calendar date, <c>yyyy-MM-dd</c>. Only Year/Month/Day
/// are written; <see cref="DateTime.Kind"/> and the time of day are ignored, with no timezone
/// conversion (the same rule the completions report's <c>from</c>/<c>to</c> follow).
/// </summary>
internal sealed class CalendarDateJsonConverter : JsonConverter<DateTime>
{
    private const string Format = "yyyy-MM-dd";

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => DateTime.ParseExact(reader.GetString() ?? "", Format, CultureInfo.InvariantCulture, DateTimeStyles.None);

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString(Format, CultureInfo.InvariantCulture));
}
