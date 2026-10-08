using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

/// <summary>Closes declared local option references before validation or Dictionary editing.</summary>
public static class RuntimeInputOptionSourceContract
{
    public static void ValidateValues(JsonObject values, JsonObject config)
    {
        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(values, config, includeHidden: true))
        {
            if (string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey)) continue;
            RuntimeInputValueKindContract.ValidateRuntimeValue(
                Close(input, values), values[input.JsonKey], $"Runtime selector '{input.Id}'");
        }
    }

    public static IReadOnlyList<FieldOption> Resolve(ComponentInputDefinition input, JsonObject values)
    {
        var key = input.OptionsSourceCollectionJsonKey;
        var items = values[key] as JsonArray
            ?? throw new InvalidOperationException($"Runtime option source '{key}' must be an array.");
        RuntimeCollectionDocumentContract.Validate(items, $"Runtime option source '{key}'");
        if (string.IsNullOrWhiteSpace(input.OptionsSourceValueJsonKey)
            || string.IsNullOrWhiteSpace(input.OptionsSourceLabelJsonKey))
            throw new InvalidOperationException($"Runtime option source '{key}' requires explicit value and label keys.");
        var result = new List<FieldOption>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in items)
        {
            var item = node!.AsObject();
            var value = JsonPath.RequiredString(item, input.OptionsSourceValueJsonKey, key);
            var label = JsonPath.RequiredString(item, input.OptionsSourceLabelJsonKey, key);
            if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(label) || !ids.Add(value))
                throw new InvalidOperationException($"Runtime option source '{key}' requires unique non-empty values and labels.");
            result.Add(new FieldOption(value, label));
        }
        return result;
    }

    public static ComponentInputDefinition Close(ComponentInputDefinition input, JsonObject values) =>
        string.IsNullOrWhiteSpace(input.OptionsSourceCollectionJsonKey) ? input : input with
        {
            Options = Resolve(input, values),
            OptionsSourceCollectionJsonKey = "",
            OptionsSourceValueJsonKey = "",
            OptionsSourceLabelJsonKey = "",
        };
}
