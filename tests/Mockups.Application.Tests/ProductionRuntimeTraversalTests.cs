using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.EditorShell;

internal static class ProductionRuntimeTraversalTests
{
    public static void DeclaredBoundariesExcludeSourceDocuments()
    {
        foreach (var runtimeKey in new[] { "inputs", "participantRuntime" })
        {
            var child = JsonNode.Parse("""
                {"actorId":"system_preview_actor_primary","inputs":[
                  {"id":"actorId","label":"Actor","jsonKey":"actorId","kind":"recordReference",
                   "valueKind":"RecordReference","tableId":"actors","defaultValue":"system_preview_actor_primary"}
                ]}
                """)!.AsObject();
            var collection = new JsonObject
            {
                ["id"] = "items", ["label"] = "Items", ["jsonKey"] = "items",
                ["itemLabel"] = "Item", ["fields"] = new JsonArray(),
            };
            if (runtimeKey == "inputs")
                collection["componentItems"] = new JsonObject
                {
                    ["variantReferenceJsonKey"] = "variantReference",
                    ["overridesJsonKey"] = "overrides", ["inputsJsonKey"] = runtimeKey,
                };
            else collection["itemRuntimeContractJsonKey"] = runtimeKey;
            var runtime = new JsonObject
            {
                ["collections"] = new JsonArray(collection),
                ["items"] = new JsonArray(new JsonObject { ["id"] = "item-a", [runtimeKey] = child.DeepClone() }),
                ["sourceSnapshot"] = child.DeepClone(),
            };
            var content = runtime.DeepClone().AsObject();
            var options = new[] { new FieldOption("actor-production", "Production actor") };
            var definition = ProductionRuntimeCreationContract.Prepare("create", content, runtime, new(), options);
            if (definition.Fields.Count != 1) throw new Exception("Only the declared child Actor must be requested.");
            var values = definition.Fields.ToDictionary(f => f.Definition.Id, _ => "actor-production");
            var completed = ProductionRuntimeCreationContract.Complete("create", content, runtime, new(), options,
                new RecordCreationDraft("create", values));
            ProductionRuntimeFixtureIsolationContract.Validate(completed, new(), "Screen");
            completed["items"]![0]![runtimeKey]!["actorId"] = "system_preview_actor_primary";
            try { ProductionRuntimeFixtureIsolationContract.Validate(completed, new(), "Screen"); }
            catch (InvalidOperationException e) when (e.Message.Contains("cannot reference System Preview Actor")) { continue; }
            throw new Exception("A Design Actor inside the declared child Runtime must still be rejected.");
        }
    }
}
