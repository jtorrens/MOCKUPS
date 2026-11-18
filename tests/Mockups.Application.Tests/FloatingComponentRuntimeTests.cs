using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.EditorShell;

internal static class FloatingComponentRuntimeTests
{
    public static void ProjectionAndReplacement()
    {
        var spec = JsonNode.Parse(File.ReadAllText("scaffolding/modules/module_core_socialPost.json"))!.AsObject();
        var declaration = spec["designPreview"]!["collections"]!.AsArray().Single(x => x!["jsonKey"]!.GetValue<string>() == "floatingComponents")!;
        var slotField = declaration["fields"]!.AsArray().Single(x => x!["jsonKey"]!.GetValue<string>() == "componentSlot")!.AsObject();
        if (RuntimeInputValueKindContract.CreateDefaultValue(slotField, "Optional slot") is not null)
            throw new Exception("Optional polymorphic slots must begin unselected.");
        var required = slotField.DeepClone().AsObject();
        required["allowEmpty"] = false;
        try { RuntimeInputValueKindContract.CreateDefaultValue(required, "Required slot"); throw new Exception("Required empty slot was accepted."); }
        catch (InvalidOperationException) { }
        var config = spec["config"]!.DeepClone().AsObject();
        var slots = config["socialPost"]!["floatingComponents"]!["items"]!.AsArray();
        foreach (var id in new[] { "bubble", "label" })
            slots.Add(new JsonObject {
                ["id"] = id, ["name"] = id,
                ["componentSlot"] = new JsonObject { ["variantReference"] = $"{id}::variant::default", ["overrides"] = new JsonObject() },
                ["placement"] = JsonNode.Parse("""{"mode":"center","alignX":0.5,"alignY":0.5,"offsetX":0,"offsetY":0}""")
            });
        JsonObject Child(string reference) => JsonNode.Parse("""
            {"inputs":[{"id":"caption","jsonKey":"text","label":"Text","kind":"text","valueKind":"StringSingleLine","defaultValue":"fixture"}],"collections":[],"text":"fixture"}
            """)!.AsObject();
        var fixture = new JsonObject { ["collections"] = new JsonArray(declaration.DeepClone()), ["floatingComponents"] = new JsonArray() };
        StructuredRuntimeCollectionProjection.Apply(fixture, config, _ => new JsonObject(), Child);
        var screen = RuntimeInputDocumentContract.CreateContentForContract(fixture, fixture);
        screen["floatingComponents"]![0]!["inputs"]!["text"] = "Screen A";
        screen["floatingComponents"]![1]!["inputs"]!["text"] = "Screen B";
        var prepared = fixture.DeepClone().AsObject();
        prepared["floatingComponents"] = screen["floatingComponents"]!.DeepClone();
        StructuredRuntimeCollectionProjection.Apply(prepared, config, _ => new JsonObject(), Child);
        if (prepared["floatingComponents"]![0]!["inputs"]!["text"]!.GetValue<string>() != "Screen A"
            || prepared["floatingComponents"]![1]!["inputs"]!["text"]!.GetValue<string>() != "Screen B")
            throw new Exception("Stored child Runtime values were overwritten by Design fixtures.");
        slots[0]!["componentSlot"]!["variantReference"] = "label::variant::alternate";
        var next = fixture.DeepClone().AsObject();
        StructuredRuntimeCollectionProjection.Apply(next, config, _ => new JsonObject(), Child);
        var reconciled = RuntimeInputDocumentContract.ReconcileContentForContract(screen, fixture, next);
        if (reconciled["floatingComponents"]![0]!["inputs"]!["text"]!.GetValue<string>() != "fixture"
            || reconciled["floatingComponents"]![1]!["inputs"]!["text"]!.GetValue<string>() != "Screen B")
            throw new Exception("Boundary replacement must reset only the changed exact Component slot.");
    }
}
