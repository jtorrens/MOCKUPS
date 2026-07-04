using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mockups.DesktopEditorShell.VisualIr;

[JsonConverter(typeof(VisualIrColorJsonConverter))]
internal abstract record VisualIrColor
{
    public static VisualIrStaticColor Static(string value)
    {
        return new VisualIrStaticColor(value);
    }

    public static VisualIrVariantColor Variant(
        IReadOnlyDictionary<string, string> values,
        string? fallback = null)
    {
        return new VisualIrVariantColor(values, fallback);
    }
}

internal sealed record VisualIrStaticColor(string Value) : VisualIrColor;

internal sealed record VisualIrVariantColor(
    IReadOnlyDictionary<string, string> Values,
    string? Fallback = null) : VisualIrColor;

internal sealed class VisualIrColorJsonConverter : JsonConverter<VisualIrColor>
{
    public override VisualIrColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return VisualIrColor.Static(reader.GetString() ?? string.Empty);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Visual IR color must be a string or a variant object.");
        }

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (!root.TryGetProperty("kind", out var kindProperty) || kindProperty.GetString() != "variant")
        {
            throw new JsonException("Unknown Visual IR color object kind.");
        }

        if (!root.TryGetProperty("values", out var valuesProperty) || valuesProperty.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Variant color requires a values object.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in valuesProperty.EnumerateObject())
        {
            values[property.Name] = property.Value.GetString() ?? string.Empty;
        }

        var fallback = root.TryGetProperty("fallback", out var fallbackProperty)
            ? fallbackProperty.GetString()
            : null;
        return VisualIrColor.Variant(values, fallback);
    }

    public override void Write(Utf8JsonWriter writer, VisualIrColor value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case VisualIrStaticColor staticColor:
                writer.WriteStringValue(staticColor.Value);
                break;
            case VisualIrVariantColor variantColor:
                writer.WriteStartObject();
                writer.WriteString("kind", "variant");
                writer.WritePropertyName("values");
                JsonSerializer.Serialize(writer, variantColor.Values, options);
                if (!string.IsNullOrWhiteSpace(variantColor.Fallback))
                {
                    writer.WriteString("fallback", variantColor.Fallback);
                }

                writer.WriteEndObject();
                break;
            default:
                throw new JsonException($"Unsupported Visual IR color type {value.GetType().Name}.");
        }
    }
}

