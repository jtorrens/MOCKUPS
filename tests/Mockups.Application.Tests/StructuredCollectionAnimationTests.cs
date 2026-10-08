using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

internal static class StructuredCollectionAnimationTests
{
    public static void ContractChangesPreserveDeclaredIdentity()
    {
        static JsonObject Field(string id, string key, string value = "Default") => new()
        {
            ["id"] = id, ["jsonKey"] = key, ["label"] = id,
            ["kind"] = "text", ["valueKind"] = "StringSingleLine", ["defaultValue"] = value,
        };
        static JsonObject Collection(string key, JsonObject field) => new()
        {
            ["id"] = "stable-collection", ["jsonKey"] = key,
            ["label"] = "Items", ["itemLabel"] = "Item",
            ["fields"] = new JsonArray(field),
        };
        var previous = new JsonObject
        {
            ["inputs"] = new JsonArray(Field("stable-field", "oldKey"), Field("removed-field", "reusedKey")),
            ["collections"] = new JsonArray(Collection("oldItems", Field("stable-item-field", "oldValue"))),
        };
        var next = new JsonObject
        {
            ["inputs"] = new JsonArray(Field("stable-field", "newKey"), Field("new-field", "reusedKey", "New default")),
            ["collections"] = new JsonArray(Collection("newItems", Field("stable-item-field", "newValue"))),
        };
        var content = new JsonObject
        {
            ["schemaVersion"] = 2, ["oldKey"] = "Authored", ["reusedKey"] = "Must not leak",
            ["oldItems"] = new JsonArray(
                new JsonObject { ["id"] = "second", ["oldValue"] = "Second authored" },
                new JsonObject { ["id"] = "first", ["oldValue"] = "First authored" }),
        };
        var result = RuntimeInputDocumentContract.ReconcileContentForContract(content, previous, next);
        Require(result["newKey"]!.GetValue<string>() == "Authored");
        Require(result["reusedKey"]!.GetValue<string>() == "New default");
        Require(!result.ContainsKey("oldKey") && !result.ContainsKey("oldItems"));
        Require(result["newItems"]![0]!["id"]!.GetValue<string>() == "second");
        Require(result["newItems"]![0]!["newValue"]!.GetValue<string>() == "Second authored");
        Require(!result["newItems"]![0]!.AsObject().ContainsKey("oldValue"));
        Require(content["oldItems"]![0]!["oldValue"]!.GetValue<string>() == "Second authored");

        // A projected collection follows the next structure, preserving values only by item id.
        var projectedPrevious = previous.DeepClone().AsObject();
        projectedPrevious["collections"]![0]!["storageCollectionJsonKey"] = "oldItems";
        var projectedNext = next.DeepClone().AsObject();
        projectedNext["collections"]![0]!["storageCollectionJsonKey"] = "newItems";
        projectedNext["newItems"] = new JsonArray(
            new JsonObject { ["id"] = "first", ["newValue"] = "Default first" },
            new JsonObject { ["id"] = "added", ["newValue"] = "Default added" });
        var projected = RuntimeInputDocumentContract.ReconcileContentForContract(content, projectedPrevious, projectedNext);
        Require(projected["newItems"]!.AsArray().Count == 2);
        Require(projected["newItems"]![0]!["id"]!.GetValue<string>() == "first");
        Require(projected["newItems"]![0]!["newValue"]!.GetValue<string>() == "First authored");
        Require(projected["newItems"]![1]!["newValue"]!.GetValue<string>() == "Default added");
    }

    public static void FixedBoundariesAndDeclaredPaths()
    {
        // A directly declared Message row and a List Item's embedded Runtime row
        // share the same lifecycle contract, including boundary-local identities.
        foreach (var wrapped in new[] { false, true })
        {
            var scalar = new JsonObject
            {
                ["id"] = "message-a.pressed", ["jsonKey"] = "pressed", ["label"] = "Pressed",
                ["kind"] = "boolean", ["valueKind"] = "Boolean", ["defaultValue"] = "false",
                ["animatable"] = true, ["animationInterpolations"] = new JsonArray("hold"),
            };
            var rows = new JsonObject
            {
                ["id"] = "rows", ["jsonKey"] = "rows", ["label"] = "Rows", ["itemLabel"] = "Row",
                ["canEditStructure"] = false, ["fixedItemCount"] = 1,
                ["fields"] = new JsonArray(scalar.DeepClone()),
            };
            var rowsField = new JsonObject
            {
                ["id"] = "rows", ["jsonKey"] = "rows", ["label"] = "Rows",
                ["kind"] = "collection", ["valueKind"] = "StructuredCollection", ["defaultValue"] = "[]",
                ["structuredCollection"] = rows,
            };
            var collection = new JsonObject
            {
                ["id"] = "items", ["jsonKey"] = "items", ["label"] = "Items", ["itemLabel"] = "Item",
                ["fields"] = wrapped ? new JsonArray() : new JsonArray(rowsField.DeepClone()),
            };
            if (wrapped) collection["itemRuntimeContractJsonKey"] = "runtime";
            var contract = new JsonObject { ["collections"] = new JsonArray(collection) };
            var definition = RuntimeInputDefinitionReader.ReadCollections(contract, new JsonObject()).Single();
            var values = new JsonObject
            {
                ["rows"] = new JsonArray(new JsonObject { ["id"] = "fixed-row", ["pressed"] = false }),
            };
            var item = new JsonObject { ["id"] = "message-a" };
            if (wrapped)
            {
                values["inputs"] = new JsonArray(rowsField.DeepClone());
                item["runtime"] = values;
            }
            else item["rows"] = values["rows"]!.DeepClone();
            var content = new JsonObject { ["items"] = new JsonArray(item) };
            const string fieldId = "rows.fixed-row.message-a.pressed";
            var animation = new JsonObject
            {
                ["schemaVersion"] = 2,
                ["tracks"] = new JsonArray(Track("t", fieldId, "message-a")),
            };
            var duplicate = StructuredCollectionMutationEngine.Apply(content, animation, definition,
                new DuplicateStructuredCollectionItem(StructuredCollectionAddress.Root("items"), "message-a"));
            var duplicatedValues = wrapped ? duplicate.Item!["runtime"]! : duplicate.Item!;
            Require(duplicatedValues["rows"]![0]!["id"]!.GetValue<string>() == "fixed-row");
            Require(!duplicate.IdentityChange.RebasedItemIds.ContainsKey("fixed-row"));
            Require(duplicate.Animation["tracks"]!.AsArray().Count == 2);
            var copyTrack = duplicate.Animation["tracks"]![1]!;
            Require(copyTrack["fieldId"]!.GetValue<string>() == fieldId);
            Require(copyTrack["targetId"]!.GetValue<string>() == duplicate.SelectedItemId);
            var prepared = contract.DeepClone().AsObject();
            prepared["items"] = duplicate.Content["items"]!.DeepClone();
            RuntimeInputAnimationValueContract.Validate(prepared, duplicate.Animation,
                new Dictionary<string, IReadOnlySet<string>>(), "Duplicated collection");
            var deleted = StructuredCollectionMutationEngine.Apply(duplicate.Content, duplicate.Animation, definition,
                new DeleteStructuredCollectionItem(StructuredCollectionAddress.Root("items"), "message-a"));
            Require(deleted.Animation["tracks"]!.AsArray().Count == 1);
            Require(deleted.Animation["tracks"]![0]!["targetId"]!.GetValue<string>() == duplicate.SelectedItemId);

            // Root structured inputs use a field path and the Screen sentinel, not item targetIds.
            var rootContract = new JsonObject { ["inputs"] = new JsonArray(rowsField.DeepClone()) };
            var rootValues = new JsonObject { ["rows"] = values["rows"]!.DeepClone() };
            var rootAnimation = new JsonObject
            {
                ["schemaVersion"] = 2,
                ["tracks"] = new JsonArray(Track("valid", fieldId), Track("orphan", "unknown", "fixed-row")),
            };
            RuntimeInputDocumentContract.RemoveOrphanedAnimationTracks(rootAnimation, rootContract, rootValues);
            Require(rootAnimation["tracks"]!.AsArray().Count == 1);
            Require(rootAnimation["tracks"]![0]!["id"]!.GetValue<string>() == "valid");
        }
    }

    private static JsonObject Track(string id, string fieldId, string? targetId = null)
    {
        var track = new JsonObject
        {
            ["id"] = id, ["fieldId"] = fieldId,
            ["keyframes"] = new JsonArray(new JsonObject
            {
                ["id"] = $"{id}-key", ["frame"] = 4, ["value"] = true,
                ["enabled"] = true, ["interpolation"] = "hold",
            }),
        };
        if (targetId is not null) track["targetId"] = targetId;
        return track;
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Collection animation contract assertion failed.");
    }
}
