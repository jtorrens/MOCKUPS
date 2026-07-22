using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

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
        var items = values[input.OptionsSourceCollectionJsonKey] as JsonArray
            ?? throw new InvalidOperationException(
                $"Runtime option source '{input.OptionsSourceCollectionJsonKey}' must be an array.");
        RuntimeCollectionDocumentContract.Validate(
            items,
            $"Runtime option source '{input.OptionsSourceCollectionJsonKey}'");
        if (string.IsNullOrWhiteSpace(input.OptionsSourceValueJsonKey)
            || string.IsNullOrWhiteSpace(input.OptionsSourceLabelJsonKey))
        {
            throw new InvalidOperationException(
                $"Runtime option source '{input.OptionsSourceCollectionJsonKey}' requires explicit value and label keys.");
        }

        var options = new List<FieldOption>(items.Count);
        var valuesSeen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index]!.AsObject();
            var owner = $"Runtime option source '{input.OptionsSourceCollectionJsonKey}' item at index {index}";
            var value = JsonPath.RequiredString(item, input.OptionsSourceValueJsonKey, owner);
            var rawLabel = JsonPath.RequiredString(item, input.OptionsSourceLabelJsonKey, owner);
            var label = rawLabel;
            if (input.OptionsSourceLabelJsonKey.Equals("variantReference", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(rawLabel))
            {
                label = optionsDataSource.RuntimeComponentVariantName(rawLabel);
            }
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(label))
            {
                throw new InvalidOperationException(
                    $"{owner} requires non-empty option value and label strings.");
            }
            if (!valuesSeen.Add(value))
            {
                throw new InvalidOperationException(
                    $"Runtime option source '{input.OptionsSourceCollectionJsonKey}' has duplicate value '{value}'.");
            }
            if (index == 0 && !string.IsNullOrWhiteSpace(input.OptionsSourceFirstItemBadge))
                label = $"{label} · {input.OptionsSourceFirstItemBadge}";
            options.Add(new FieldOption(value, label));
        }
        return options;
    }
}
