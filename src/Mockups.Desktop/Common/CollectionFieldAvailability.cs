using System;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class CollectionFieldAvailability
{
    public static bool IsEnabled(
        JsonObject item,
        ComponentInputDefinition input,
        int itemIndex = 0)
    {
        if (itemIndex < input.MinimumItemIndex) return false;
        if (string.IsNullOrWhiteSpace(input.EnabledWhenItemJsonKey)
            || input.EnabledWhenItemValues is not { Count: > 0 })
        {
            return true;
        }

        var current = item[input.EnabledWhenItemJsonKey] is JsonValue value
            && value.TryGetValue<string>(out var text)
            ? text
            : "";
        return input.EnabledWhenItemValues.Contains(current, StringComparer.Ordinal);
    }

    public static bool AllowsEmpty(JsonObject item, ComponentInputDefinition input)
    {
        if (string.IsNullOrWhiteSpace(input.AllowEmptyWhenItemJsonKey)
            || input.AllowEmptyWhenItemValues is not { Count: > 0 })
        {
            return input.AllowEmpty;
        }

        var current = item[input.AllowEmptyWhenItemJsonKey] is JsonValue value
            && value.TryGetValue<string>(out var text)
            ? text
            : "";
        return input.AllowEmptyWhenItemValues.Contains(current, StringComparer.Ordinal);
    }
}
