using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.EditorShell;

internal static class ProjectedChildRuntimeTests
{
    public static void CompleteContractsHaveOneOwner()
    {
        foreach (var prefix in new[] { "slots", "panels" })
        {
            var sourceRuntime = JsonNode.Parse("""
                {"inputs":[
                  {"id":"actor-field","jsonKey":"actorId","label":"Actor","kind":"recordReference","valueKind":"RecordReference","tableId":"actors","resolvedJsonKey":"actor","defaultValue":"saved-actor"},
                  {"id":"caption-field","jsonKey":"caption","label":"Caption","kind":"text","valueKind":"StringSingleLine","defaultValue":"saved caption"}
                ],"actorId":"saved-actor","caption":"saved caption","collections":[]}
                """)!.AsObject();
            var structural = new JsonArray(new JsonObject
            {
                ["id"] = "parent-01", ["title"] = "First",
                ["choices"] = new JsonArray(new JsonObject
                {
                    ["id"] = "child-01", ["variant"] = "class-a::variant::default",
                    ["values"] = sourceRuntime.DeepClone(),
                }),
            });
            var projection = JsonNode.Parse("""
                {"optionsSourceCollectionJsonKey":"choices","stateJsonKey":"selected",
                 "transitionJsonKey":"changing","elapsedJsonKey":"elapsed","fromJsonKey":"previous",
                 "itemMetadataJsonKeys":["title"],"childCollection":{
                   "id":"children","label":"Children","jsonKey":"childValues","itemLabel":"Child",
                   "sourceCollectionJsonKey":"choices","parentItemIdJsonKey":"parent",
                   "variantReferenceJsonKey":"variant","runtimeContractJsonKey":"runtime",
                   "sourceRuntimeContractJsonKey":"values","fields":[
                     {"id":"parent","jsonKey":"parent","label":"Parent","kind":"text","valueKind":"StringSingleLine","defaultValue":""},
                     {"id":"variant","jsonKey":"variant","label":"Variant","kind":"text","valueKind":"StringSingleLine","defaultValue":""}]}}
                """)!.AsObject();
            var field = JsonNode.Parse("""
                {"id":"selected","label":"Selected","jsonKey":"selected","kind":"text","valueKind":"StringSingleLine","defaultValue":"child-01"}
                """)!;
            var config = new JsonObject
            {
                ["items"] = structural,
                [RuntimeInputForwardingContract.StorageKey] = new JsonObject
                {
                    ["items"] = new JsonObject
                    {
                        ["id"] = $"forwarded.{prefix}", ["jsonKey"] = prefix,
                        ["collection"] = new JsonObject
                        {
                            ["id"] = prefix, ["label"] = prefix, ["jsonKey"] = prefix,
                            ["itemLabel"] = "Parent", ["fields"] = new JsonArray(field),
                        },
                        ["projection"] = projection,
                    },
                },
            };
            var prepared = RuntimeInputForwardingContract.EffectivePreview(new JsonObject(), config);
            var child = prepared["childValues"]![0]!["runtime"]!.AsObject();
            var fields = RuntimeInputDefinitionReader.ReadInputs(child, new JsonObject());
            if (fields.Count != 2 || !fields.Any(f => f.Id == "actor-field"))
                throw new Exception("The complete declared child Runtime contract must reach Screen Payload.");
            var content = RuntimeInputDocumentContract.CreateContentForContract(prepared, prepared);
            var runtimeJson = DesignPreviewTestValues.Parse(DesignPreviewTestValues.RuntimeJson(prepared.ToJsonString()));
            if (runtimeJson[prefix]![0]!["choices"] is not null)
                throw new Exception("Runtime conversion cannot reintroduce the structural source.");
            if (content[prefix]![0]!["choices"] is not null)
                throw new Exception("Variant structure cannot be a second persisted Runtime owner.");
            content["childValues"]![0]!["runtime"]!["caption"] = "Screen caption";
            var refreshed = RuntimeInputForwardingContract.EffectivePreview(content, config);
            if (refreshed["childValues"]![0]!["runtime"]!["caption"]!.GetValue<string>() != "Screen caption"
                || sourceRuntime["caption"]!.GetValue<string>() != "saved caption")
                throw new Exception("Screen edits must survive projection without changing Design defaults.");
            var owners = RuntimeInputContractTraversal.Contracts(refreshed, config, []).ToList();
            if (owners.Count != 2)
                throw new Exception("Creation and Preview must visit each declared Runtime owner once.");
            projection["itemMetadataJsonKeys"]!.AsArray().Add("choices");
            try
            {
                RuntimeInputForwardingContract.EffectivePreview(new JsonObject(), config);
                throw new Exception("Duplicating a child Runtime owner as parent metadata was accepted.");
            }
            catch (InvalidOperationException error) when (error.Message.Contains("cannot also be parent item metadata", StringComparison.Ordinal)) { }
        }
    }
}
