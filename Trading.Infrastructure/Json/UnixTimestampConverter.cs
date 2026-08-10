using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trading.Infrastructure.Json;

/// <summary>
/// Converts between DateTime and Unix epoch seconds. Server payloads emit
/// numeric epoch timestamps, whereas the C# DTOs expose DateTime.
/// </summary>
public sealed class UnixTimestampConverter : JsonConverter<DateTime>
{
    private static readonly DateTimeOffset Epoch = DateTimeOffset.UnixEpoch;

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            var seconds = reader.GetInt64();
            return Epoch.AddSeconds(seconds).UtcDateTime;
        }

        if (reader.TokenType == JsonTokenType.String &&
            DateTime.TryParse(reader.GetString(), out var parsed))
        {
            return parsed;
        }

        return default;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var seconds = (long)(value.ToUniversalTime() - Epoch.DateTime).TotalSeconds;
        writer.WriteNumberValue(seconds);
    }
}