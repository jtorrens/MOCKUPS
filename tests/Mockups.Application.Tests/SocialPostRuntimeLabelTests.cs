using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.EditorShell;

internal static class SocialPostRuntimeLabelTests
{
    public static void ScreenLabelsSurviveProjection()
    {
        var spec = JsonNode.Parse(File.ReadAllText("scaffolding/modules/module_core_socialPost.json"))!.AsObject();
        var iconRow = JsonNode.Parse(File.ReadAllText("scaffolding/components/iconRow.json"))!["config"]!.AsObject();
        var config = spec["additionalVariants"]!.AsArray().Select(v => v!.AsObject())
            .Single(v => v["id"]!.GetValue<string>() == "reel")["config"]!.AsObject();
        var defaults = RuntimePreviewDocumentContract.PrepareDeclaredInputDefaults(
            spec["designPreview"]!.AsObject(), config);
        if (defaults["mediaSource"] is not JsonValue mediaSource
            || !mediaSource.TryGetValue<string>(out var emptySource) || emptySource != "")
            throw new InvalidOperationException("Empty Media source must reach Preview as the Media owner's empty string.");
        var declaration = spec["designPreview"]!["collections"]!.AsArray().Select(v => v!.AsObject())
            .Single(v => v["id"]!.GetValue<string>() == "socialPostActions");
        var screens = new[] { "Screen A", "Screen B", "" }.Select(text => {
            var doc = new JsonObject {
                ["collections"] = new JsonArray(declaration.DeepClone()),
                ["socialPostActions"] = spec["designPreview"]!["socialPostActions"]!.DeepClone()
            };
            doc["socialPostActions"]![0]!["sampleText"] = text;
            return doc;
        }).ToArray();
        var firstId = screens[0]["socialPostActions"]![0]!["id"]!.GetValue<string>();
        var changedConfig = config.DeepClone().AsObject();
        var items = changedConfig["socialPost"]!["actionsSlot"]!["overrides"]!["iconRow"]!["items"]!.AsArray();
        var first = items[0]!.DeepClone();
        items.RemoveAt(0);
        first["text"] = "Variant text must not overwrite Screen text";
        items.Add(first);
        for (var index = 0; index < screens.Length; index++) {
            StructuredRuntimeCollectionProjection.Apply(screens[index], changedConfig, _ => iconRow.DeepClone().AsObject());
            var projected = screens[index]["socialPostActions"]!.AsArray();
            if (projected.Last()!["id"]!.GetValue<string>() != firstId
                || projected.Last()!["sampleText"]!.GetValue<string>() != new[] { "Screen A", "Screen B", "" }[index])
                throw new InvalidOperationException("Screen label was overwritten or matched by position.");
        }
        var footer = spec["designPreview"]!["collections"]!.AsArray().Select(v => v!.AsObject())
            .Single(v => v["id"]!.GetValue<string>() == "socialPostFooterRows");
        var slots = footer["fields"]!.AsArray().Single(v => v!["id"]!.GetValue<string>() == "slotInputs")!["structuredCollection"]!;
        var label = slots["fields"]!.AsArray().Single(v => v!["id"]!.GetValue<string>() == "label")!;
        if (!label["enabledWhenItemValues"]!.AsArray().Any(v => v!.GetValue<string>() == "icon"))
            throw new InvalidOperationException("Footer Button label is not editable.");
    }
}
