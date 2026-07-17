using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class RuntimeInputDynamicOptions
{
    public static IReadOnlyList<FieldOption>? Resolve(
        RuntimeInputOptionsDataSource optionsDataSource,
        ComponentInputDefinition? input,
        JsonObject values)
    {
        if (input is null) return null;
        if (string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey)) return input.Options;
        if (values[input.OptionsSourceCollectionJsonKey] is not JsonArray items) return [];
        return items.OfType<JsonObject>().Select((item, index) =>
        {
            var value = item[input.OptionsSourceValueJsonKey]?.GetValue<string>() ?? "";
            var rawLabel = string.IsNullOrWhiteSpace(input.OptionsSourceLabelJsonKey)
                ? ""
                : item[input.OptionsSourceLabelJsonKey]?.GetValue<string>() ?? "";
            var label = rawLabel;
            if (input.OptionsSourceLabelJsonKey.Equals("presetId", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(rawLabel))
            {
                try { label = optionsDataSource.RuntimeComponentPresetName(rawLabel); }
                catch { label = rawLabel; }
            }
            if (input.OptionsSourceLabelJsonKey.Equals("name", StringComparison.Ordinal)
                && string.IsNullOrWhiteSpace(label))
            {
                throw new InvalidOperationException(
                    $"Runtime option '{value}' is missing its required name in '{input.OptionsSourceCollectionJsonKey}'.");
            }
            if (string.IsNullOrWhiteSpace(label)) label = $"State {index + 1}";
            if (index == 0 && !string.IsNullOrWhiteSpace(input.OptionsSourceFirstItemBadge))
                label = $"{label} · {input.OptionsSourceFirstItemBadge}";
            return new FieldOption(value, label);
        }).Where((option) => !string.IsNullOrWhiteSpace(option.Value)).ToList();
    }
}
