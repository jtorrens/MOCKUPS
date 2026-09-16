using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class ProductionRuntimeFixtureIsolationContract
{
    public static void Validate(
        JsonObject runtime,
        JsonObject config,
        string owner)
    {
        ValidateContract(runtime, config, owner);
        VisitNestedContracts(runtime, owner);
    }

    private static void ValidateContract(
        JsonObject runtime,
        JsonObject config,
        string owner)
    {
        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(
                     runtime,
                     config,
                     includeHidden: true))
        {
            if (input.Source != ComponentInputSource.Runtime
                || !CollectionFieldAvailability.IsEnabled(runtime, input)
                || !runtime.TryGetPropertyValue(input.JsonKey, out var value))
            {
                continue;
            }
            ValidateValue(input, value, $"{owner} Runtime Input '{input.Id}'");
        }

        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(
                     runtime,
                     config,
                     includeHidden: true))
        {
            if (runtime[collection.JsonKey] is not JsonArray items) continue;
            foreach (var item in items.OfType<JsonObject>())
            {
                foreach (var field in collection.Fields.Where((field) =>
                             field.Source == ComponentInputSource.Runtime
                             && CollectionFieldAvailability.IsEnabled(item, field)))
                {
                    if (item.TryGetPropertyValue(field.JsonKey, out var value))
                    {
                        ValidateValue(
                            field,
                            value,
                            $"{owner} Runtime collection '{collection.Id}' field '{field.Id}'");
                    }
                }
            }
        }
    }

    private static void ValidateValue(
        ComponentInputDefinition input,
        JsonNode? value,
        string owner)
    {
        if (value is JsonValue scalar
            && scalar.TryGetValue<string>(out var text))
        {
            if (input.ValueKind == ValueKind.RecordReference
                && input.TableId == "actors"
                && SystemPreviewFixtureCatalog.IsActor(text))
            {
                throw new InvalidOperationException(
                    $"{owner} cannot reference System Preview Actor '{text}'.");
            }
        }

        if (input.ValueKind != ValueKind.StructuredCollection
            || input.StructuredCollection is null
            || value is not JsonArray items)
        {
            return;
        }
        foreach (var item in items.OfType<JsonObject>())
        {
            foreach (var field in input.StructuredCollection.Fields.Where((field) =>
                         field.Source == ComponentInputSource.Runtime
                         && CollectionFieldAvailability.IsEnabled(item, field)))
            {
                if (item.TryGetPropertyValue(field.JsonKey, out var nested))
                {
                    ValidateValue(field, nested, $"{owner} field '{field.Id}'");
                }
            }
        }
    }

    private static void VisitNestedContracts(JsonNode? node, string owner)
    {
        if (node is JsonArray array)
        {
            foreach (var child in array) VisitNestedContracts(child, owner);
            return;
        }
        if (node is not JsonObject value) return;

        if (value["inputs"] is JsonArray || value["collections"] is JsonArray)
        {
            ValidateContract(value, new JsonObject(), owner);
        }
        foreach (var (_, child) in value)
        {
            VisitNestedContracts(child, owner);
        }
    }
}
