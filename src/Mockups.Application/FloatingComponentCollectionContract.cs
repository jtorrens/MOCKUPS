using System;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal static class FloatingComponentCollectionContract
{
    internal static void Validate(JsonObject value, string owner)
    {
        if (value.Count != 2 || !value.ContainsKey("items") || !value.ContainsKey("motion"))
            throw new InvalidOperationException($"{owner} requires exactly items and motion.");
        _ = MotionVariantValue.Parse(JsonPath.RequiredObject(value, "motion", owner).ToJsonString());
        var items = JsonPath.RequiredArray(value, "items", owner);
        RuntimeCollectionDocumentContract.Validate(items, owner);
        foreach (var node in items)
        {
            var item = node!.AsObject();
            if (item.Count != 4 || item.Any(pair => pair.Key is not ("id" or "name" or "componentSlot" or "placement")))
                throw new InvalidOperationException($"{owner} item requires id, name, componentSlot and placement.");
            JsonPath.RequiredString(item, "name", owner);
            _ = AlignmentPlacementValue.Parse(JsonPath.RequiredObject(item, "placement", owner).ToJsonString());
            if (!item.ContainsKey("componentSlot")) throw new InvalidOperationException($"{owner} item requires componentSlot.");
            if (item["componentSlot"] is not null)
                ComponentVariantSlotDocumentContract.Validate(JsonPath.RequiredObject(item, "componentSlot", owner), owner);
        }
    }
}
