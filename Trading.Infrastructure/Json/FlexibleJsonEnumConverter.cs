namespace Trading.Infrastructure.Json;

/// <summary>
/// A forgiving enum converter: reads snake_case/camel/Pascal case strings
/// (falling back to a default value), and writes values as snake_case.
/// Keeps the client resilient to small differences in server payloads.
/// </summary>
public sealed class FlexibleJsonEnumConverter<TEnum> :
    System.Text.Json.Serialization.JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    public override TEnum Read(
        ref System.Text.Json.Utf8JsonReader reader,
        Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
            return default;

        var cleaned = value.Trim();

        if (Enum.TryParse(cleaned, out TEnum exact))
            return exact;

        if (Enum.TryParse(cleaned, ignoreCase: true, out TEnum ci))
            return ci;

        foreach (var name in Enum.GetNames<TEnum>())
        {
            if (string.Equals(name, cleaned, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ToSnakeCase(name), cleaned, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ToCamelCase(name), cleaned, StringComparison.OrdinalIgnoreCase))
            {
                return Enum.Parse<TEnum>(name);
            }
        }

        return default;
    }

    public override void Write(
        System.Text.Json.Utf8JsonWriter writer,
        TEnum value,
        System.Text.Json.JsonSerializerOptions options)
    {
        writer.WriteStringValue(ToSnakeCase(value.ToString()));
    }

    private static string ToSnakeCase(string name)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string ToCamelCase(string name)
    {
        if (name.Length == 0)
            return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}