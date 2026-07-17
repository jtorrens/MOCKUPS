using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class NavigationBarComponentConfigContract
{
    public const string ComponentType = "navigation_bar";

    public static void Validate(JsonObject config, string context)
    {
        RequireExact(JsonPath.RequiredInteger(config, "schemaVersion", context), 1, $"{context}.schemaVersion");
        RequireOneOf(JsonPath.RequiredString(config, "type", context), ["buttons", "gestureBar"], $"{context}.type");
        JsonPath.RequiredString(config, "foregroundColorToken", context);
        JsonPath.RequiredString(config, "backgroundColorToken", context);
        RequireRange(JsonPath.RequiredNumber(config, "backgroundAlpha", context), 0, 1, $"{context}.backgroundAlpha");

        var layout = JsonPath.RequiredObject(config, "layout", context);
        JsonPath.RequiredNumber(layout, "height", $"{context}.layout");
        JsonPath.RequiredNumber(layout, "itemSize", $"{context}.layout");
        JsonPath.RequiredString(layout, "sidePadding", $"{context}.layout");
        JsonPath.RequiredNumber(layout, "strokeWidth", $"{context}.layout");
        JsonPath.RequiredNumber(layout, "cornerRadius", $"{context}.layout");
        JsonPath.RequiredBoolean(layout, "filled", $"{context}.layout");

        var gesture = JsonPath.RequiredObject(config, "gesture", context);
        JsonPath.RequiredNumber(gesture, "width", $"{context}.gesture");
        JsonPath.RequiredNumber(gesture, "height", $"{context}.gesture");
        JsonPath.RequiredNumber(gesture, "cornerRadius", $"{context}.gesture");

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var items = JsonPath.RequiredArray(config, "items", context);
        for (var index = 0; index < items.Count; index++)
        {
            var itemContext = $"{context}.items[{index}]";
            var item = items[index] as JsonObject
                ?? throw new InvalidOperationException($"{itemContext} must be an object.");
            var id = JsonPath.RequiredString(item, "id", itemContext);
            if (!ids.Add(id))
            {
                throw new InvalidOperationException($"{context}.items contains duplicate stable id '{id}'.");
            }

            JsonPath.RequiredString(item, "label", itemContext);
            RequireOneOf(
                JsonPath.RequiredString(item, "kind", itemContext),
                ["generatedBack", "generatedHome", "generatedRecents"],
                $"{itemContext}.kind");
            RequireOneOf(
                JsonPath.RequiredString(item, "zone", itemContext),
                ["off", "left", "center", "right"],
                $"{itemContext}.zone");
            JsonPath.RequiredInteger(item, "order", itemContext);
        }
    }

    private static void RequireExact(int value, int expected, string path)
    {
        if (value != expected)
        {
            throw new InvalidOperationException($"{path} must equal {expected}.");
        }
    }

    private static void RequireRange(double value, double minimum, double maximum, string path)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidOperationException($"{path} must be between {minimum} and {maximum}.");
        }
    }

    private static void RequireOneOf(string value, IReadOnlyList<string> options, string path)
    {
        if (!options.Contains(value, StringComparer.Ordinal))
        {
            throw new InvalidOperationException($"{path} has unsupported value '{value}'.");
        }
    }
}
