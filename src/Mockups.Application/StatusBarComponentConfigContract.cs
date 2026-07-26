using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal static class StatusBarComponentConfigContract
{
    public const string ComponentType = "status_bar";

    public static void Validate(JsonObject config, string context)
    {
        RequireExact(JsonPath.RequiredInteger(config, "schemaVersion", context), 2, $"{context}.schemaVersion");
        JsonPath.RequiredString(config, "foregroundColorToken", context);
        JsonPath.RequiredString(config, "backgroundColorToken", context);
        RequireRange(JsonPath.RequiredNumber(config, "backgroundAlpha", context), 0, 1, $"{context}.backgroundAlpha");

        var layout = JsonPath.RequiredObject(config, "layout", context);
        JsonPath.RequiredNumber(layout, "height", $"{context}.layout");
        JsonPath.RequiredNumber(layout, "itemSize", $"{context}.layout");
        JsonPath.RequiredString(layout, "gap", $"{context}.layout");
        JsonPath.RequiredString(layout, "sidePadding", $"{context}.layout");

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
            var kind = JsonPath.RequiredString(item, "kind", itemContext);
            RequireOneOf(kind, ["text", "iconToken", "generatedBattery", "generatedSignal"], $"{itemContext}.kind");
            RequireOneOf(JsonPath.RequiredString(item, "zone", itemContext), ["off", "left", "right"], $"{itemContext}.zone");
            JsonPath.RequiredInteger(item, "order", itemContext);

            switch (kind)
            {
                case "text":
                    JsonPath.RequiredString(item, "value", itemContext, allowEmpty: true);
                    break;
                case "iconToken":
                    JsonPath.RequiredString(item, "token", itemContext);
                    break;
                case "generatedSignal":
                    RequireRange(JsonPath.RequiredInteger(item, "value", itemContext), 0, 4, $"{itemContext}.value");
                    break;
                case "generatedBattery":
                    RequireRange(JsonPath.RequiredInteger(item, "value", itemContext), 0, 100, $"{itemContext}.value");
                    JsonPath.RequiredBoolean(item, "charging", itemContext);
                    break;
            }
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
