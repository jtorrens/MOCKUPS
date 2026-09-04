using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

public static class OverrideDocumentContract
{
    public static bool HasAuthoredValues(JsonObject overrides) =>
        HasAuthoredValue(overrides);

    private static bool HasAuthoredValue(JsonNode? node) => node switch
    {
        JsonObject objectValue => objectValue.Any((entry) =>
            HasAuthoredValue(entry.Value)),
        JsonArray => true,
        not null => true,
        _ => false,
    };
}
