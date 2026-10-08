using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class RuntimeInputDynamicOptions
{
    public static IReadOnlyList<FieldOption>? Resolve(
        IRuntimeInputOptionsDataSource optionsDataSource,
        ComponentInputDefinition? input,
        JsonObject values)
    {
        if (input is null) return null;
        if (string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey)) return input.Options;
        var items = RuntimeInputOptionSourceContract.Resolve(input, values);
        var options = new List<FieldOption>(items.Count);
        for (var index = 0; index < items.Count; index++)
        {
            var value = items[index].Value;
            var rawLabel = items[index].Label;
            var label = rawLabel;
            if (input.OptionsSourceLabelJsonKey.Equals("variantReference", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(rawLabel))
            {
                label = optionsDataSource.RuntimeComponentVariantName(rawLabel);
            }
            if (index == 0 && !string.IsNullOrWhiteSpace(input.OptionsSourceFirstItemBadge))
                label = $"{label} · {input.OptionsSourceFirstItemBadge}";
            options.Add(new FieldOption(value, label));
        }
        return options;
    }

    public static IReadOnlyList<FieldOption>? ResolveForCollectionItem(
        IRuntimeInputOptionsDataSource optionsDataSource,
        ComponentInputDefinition? input,
        JsonObject preview,
        IReadOnlyDictionary<string, RuntimeInputCollectionDefinition> collections,
        RuntimeInputCollectionDefinition collection,
        JsonObject item)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey))
        {
            return Resolve(optionsDataSource, input, item);
        }
        var sourceOwner = RuntimeCollectionItemContext.ResolveOwnerOfKey(
            preview,
            collections,
            collection,
            item,
            input.OptionsSourceCollectionJsonKey,
            $"Runtime option source '{input.OptionsSourceCollectionJsonKey}'");
        return Resolve(optionsDataSource, input, sourceOwner);
    }
}
