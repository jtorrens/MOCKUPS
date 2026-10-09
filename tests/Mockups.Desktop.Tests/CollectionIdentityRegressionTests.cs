using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;

internal static class CollectionIdentityRegressionTests
{
    public static void PreparedNestedTargetsSurviveMutations(string sourcePath)
    {
        var temporary = Path.Combine(Path.GetTempPath(), $"mockups-collection-identity-{Guid.NewGuid():N}.sqlite");
        File.Copy(sourcePath, temporary);
        try
        {
            var database = new SqliteProjectTestContext(temporary);
            var nodes = Descendants(database.LoadProjectTree()).DistinctBy(node => (node.Kind, node.Id)).ToArray();
            var source = nodes.Single(node => node.Kind == ProjectTreeNodeKind.ComponentVariant
                && node.Id == "component_project_foqn_s2_iconRow::variant::default");
            var variant = database.DuplicateComponentVariant(source, "Nested identity regression");
            var config = database.GetComponentVariantConfig(variant.Id);
            var row = config["iconRow"]!.AsObject();
            var buttonsDefinition = ComponentClassFieldCatalog.Get("component.iconRow.items").StructuredCollection!;
            var added = StructuredCollectionMutationEngine.Apply(row, EmptyAnimation(), buttonsDefinition,
                new DuplicateStructuredCollectionItem(StructuredCollectionAddress.Root("items"),
                    row["items"]![0]!["id"]!.GetValue<string>()));
            row["items"] = added.Collection.DeepClone();
            database.ReplaceComponentVariantConfig(variant, config.ToJsonString());
            var screens = nodes.Where(node => node.Kind == ProjectTreeNodeKind.ModuleInstance
                && database.GetModuleInstanceSettings(node.Id).ModuleId == "module_core_chat").Take(2).ToArray();
            Require(screens.Length == 2);
            foreach (var screen in screens)
            {
                var content = JsonNode.Parse(database.GetModuleInstanceSettings(screen.Id).ContentJson)!.AsObject();
                var message = content["messages"]![0]!.AsObject();
                var messageId = message["id"]!.GetValue<string>();
                var rows = message["iconRowRuntime"]!.DeepClone().AsArray();
                rows[0]!["iconRowSlot"] = ComponentVariantSlotDocumentContract.Create(variant.Id, new JsonObject(), "Regression row");
                rows[0]!["runtimeInputs"] = database.GetComponentVariantRuntimeContract(variant.Id);
                database.UpdateModuleInstanceRuntimeCollectionValue(screen.Id, "messages", messageId, "iconRowRuntime", rows);
                var runtime = JsonNode.Parse(database.GetModuleInstanceRuntimePreviewJson(screen.Id))!.AsObject();
                var target = RuntimeInputAnimationValueContract.ReadTargets(runtime, new JsonObject(), runtime)
                    .Single(target => target.TargetId == messageId && target.Input.Id == "pressed"
                        && target.FieldPath.Any(segment => segment.IsItem && segment.Id == added.SelectedItemId));
                var animation = new ModuleInstanceAnimationDocument(database.GetModuleInstanceSettings(screen.Id).AnimationJson);
                animation.AddTrack(target.FieldId, target.TargetId, JsonValue.Create(false)!, "hold");
                animation.UpsertKeyframe(target.FieldId, target.TargetId, 9, JsonValue.Create(true)!, "hold");
                database.UpdateModuleInstanceAnimationJson(screen.Id, animation.ToJson());

                // Negative control reproduces a persisted nested payload whose structure
                // disagrees with its exact Variant. Only this disposable database is changed.
                content = JsonNode.Parse(database.GetModuleInstanceSettings(screen.Id).ContentJson)!.AsObject();
                var nested = content["messages"]![0]!["iconRowRuntime"]![0]!["runtimeInputs"]!["buttonInputs"]!.AsArray();
                nested.Remove(nested.Single(item => item!["id"]!.GetValue<string>() == added.SelectedItemId));
                var contract = JsonNode.Parse(database.GetModuleInstanceEffectiveContractJson(screen.Id))!.AsObject();
                var definition = RuntimeInputDefinitionReader.ReadCollections(contract, new JsonObject(), includeHidden: true)
                    .Single(item => item.StorageJsonKey == "messages");
                var address = StructuredCollectionAddress.Root("messages");
                var raw = StructuredCollectionMutationEngine.Apply(content, JsonNode.Parse(animation.ToJson())!.AsObject(),
                    definition, new DeleteStructuredCollectionItem(address, messageId));
                Require(raw.Animation["tracks"]!.AsArray().Any(track => track!["fieldId"]!.GetValue<string>() == target.FieldId));
                using (var connection = new SqliteConnection($"Data Source={temporary}"))
                {
                    connection.Open();
                    using var write = connection.CreateCommand();
                    write.CommandText = "UPDATE module_instances SET content_json = $content WHERE id = $id";
                    write.Parameters.AddWithValue("$content", content.ToJsonString());
                    write.Parameters.AddWithValue("$id", screen.Id);
                    Require(write.ExecuteNonQuery() == 1);
                }
                var duplicated = database.MutateModuleInstanceStructuredCollection(screen.Id,
                    new DuplicateStructuredCollectionItem(address, messageId));
                Require(duplicated.SelectedItemId!.StartsWith("item_", StringComparison.Ordinal));
                Require(Guid.TryParseExact(duplicated.SelectedItemId[5..], "N", out _));
                var duplicatedAnimation = duplicated.Animation.ToJsonString();
                var moved = database.MutateModuleInstanceStructuredCollection(screen.Id,
                    new MoveStructuredCollectionItem(address, duplicated.SelectedItemId, messageId));
                Require(moved.Animation.ToJsonString() == duplicatedAnimation);
                Require(moved.Collection[0]!["id"]!.GetValue<string>() == duplicated.SelectedItemId);
                var deleted = database.MutateModuleInstanceStructuredCollection(screen.Id,
                    new DeleteStructuredCollectionItem(address, messageId));
                Require(!deleted.Animation["tracks"]!.AsArray().Any(track => track?["targetId"]?.GetValue<string>() == messageId));
                Require(deleted.Animation["tracks"]!.AsArray().Any(track => track?["targetId"]?.GetValue<string>() == duplicated.SelectedItemId));
            }
            _ = new SqliteProjectTestContext(temporary);
        }
        finally { File.Delete(temporary); }
    }

    private static JsonObject EmptyAnimation() => new() { ["schemaVersion"] = 2, ["tracks"] = new JsonArray() };
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Collection identity regression failed.");
    }
    private static IEnumerable<ProjectTreeNode> Descendants(IEnumerable<ProjectTreeNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Descendants(node.Children)) yield return child;
        }
    }
}
