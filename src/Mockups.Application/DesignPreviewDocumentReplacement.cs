using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.EditorShell;

// A replacement is explicitly conditional on the authored revision it read.
public sealed record DesignPreviewDocumentReplacement(string ExpectedJson, string ProposedJson)
{
    public JsonObject Validate(string currentJson, JsonObject config, string owner)
    {
        var current = JsonPath.ParseRequiredObject(currentJson, owner);
        var expected = JsonPath.ParseRequiredObject(ExpectedJson, owner);
        if (!JsonNode.DeepEquals(current, expected))
            throw new InvalidOperationException($"{owner} changed after preparation. Reload before saving.");
        var proposed = JsonPath.ParseRequiredObject(ProposedJson, owner);
        var missing = current.Select(pair => pair.Key).Except(proposed.Select(pair => pair.Key)).ToArray();
        // Test Values may be promoted into their declared defaults by the authoring owner.
        if (missing.Any(key => key != "testValues"))
            throw new InvalidOperationException($"{owner} replacement removes required document fields.");
        var originalInputs = RuntimeInputDefinitionReader.ReadInputs(current, config, includeHidden: true);
        var inputs = RuntimeInputDefinitionReader.ReadInputs(proposed, config, includeHidden: true);
        if (JsonSerializer.Serialize(originalInputs.Select(input => input with { DefaultValue = "" }))
            != JsonSerializer.Serialize(inputs.Select(input => input with { DefaultValue = "" })))
            throw new InvalidOperationException($"{owner} replacement cannot change the Runtime declaration contract.");
        foreach (var input in inputs.Where(input => input.Source == ComponentInputSource.Runtime))
            RuntimeInputValueKindContract.ValidateRuntimeValue(input,
                DesignPreviewTestValues.ValueNode(input, DesignPreviewTestValues.Value(proposed, input)), owner);
        var collections = RuntimeInputDefinitionReader.ReadCollections(proposed, config, includeHidden: true);
        var originalCollections = RuntimeInputDefinitionReader.ReadCollections(current, config, includeHidden: true);
        if (JsonSerializer.Serialize(originalCollections) != JsonSerializer.Serialize(collections))
            throw new InvalidOperationException($"{owner} replacement cannot change collection declarations.");
        foreach (var collection in collections)
            _ = StructuredCollectionDocumentContract.EffectiveAuthoringClone(
                new JsonArray(DesignPreviewTestValues.CollectionItems(proposed, collection)
                    .Select(item => (JsonNode?)item.DeepClone()).ToArray()), collection, owner);
        DesignPreviewTestValues.ValidateFixedCollectionCounts(proposed, config, owner);
        ComponentPreviewActions.ValidateContract(proposed, owner);
        return proposed;
    }
}
