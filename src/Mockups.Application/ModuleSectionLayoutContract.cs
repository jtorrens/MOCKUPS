using System;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal static class ModuleSectionLayoutContract
{
    internal static void Validate(JsonObject owner, string key, string context)
    {
        var value = JsonPath.RequiredObject(owner, key, context);
        if (value.Count != 3 || !new[] { "mode", "padding", "placement" }.All(value.ContainsKey))
            throw new InvalidOperationException($"{context}.{key} requires exactly mode, padding and placement.");
        if (JsonPath.RequiredString(value, "mode", context) is not ("stack" or "float"))
            throw new InvalidOperationException($"{context}.{key}.mode must be stack or float.");
        var padding = JsonPath.RequiredString(value, "padding", context).Split('|');
        if (padding.Length != 2 || padding.Any(token => !token.StartsWith("theme.spacing.", StringComparison.Ordinal) || token.Length == "theme.spacing.".Length))
            throw new InvalidOperationException($"{context}.{key}.padding requires two spacing tokens.");
        _ = AlignmentPlacementValue.Parse(JsonPath.RequiredObject(value, "placement", context).ToJsonString());
    }
}
