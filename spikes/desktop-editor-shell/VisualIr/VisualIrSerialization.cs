using System.Text.Json;

namespace Mockups.DesktopEditorShell.VisualIr;

internal static class VisualIrSerialization
{
    public static JsonSerializerOptions CreateOptions(bool writeIndented = false)
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = writeIndented,
        };
    }

    public static string Serialize(VisualIrDocument document, bool writeIndented = false)
    {
        return JsonSerializer.Serialize(document, CreateOptions(writeIndented));
    }

    public static VisualIrDocument Deserialize(string json)
    {
        return JsonSerializer.Deserialize<VisualIrDocument>(json, CreateOptions())
            ?? throw new JsonException("Visual IR document could not be deserialized.");
    }
}

