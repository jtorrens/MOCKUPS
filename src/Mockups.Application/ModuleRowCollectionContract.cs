using System;
using System.Linq;
using System.Text.Json.Nodes;
using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.Data;

internal static class ModuleRowCollectionContract
{
    internal static void Validate(
        JsonObject document,
        string key,
        string owner)
    {
        var rows = JsonPath.RequiredArray(document, key, owner);
        RuntimeCollectionDocumentContract.Validate(rows, $"{owner}.{key}");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex] as JsonObject
                ?? throw new InvalidOperationException(
                    $"{owner}.{key}[{rowIndex}] must be an object.");
            var rowId = JsonPath.RequiredString(row, "id", owner);
            if (row.Count != 5 || row.Any(pair => pair.Key is not ("id" or "label" or "visible" or "rowSlot" or "placement")))
                throw new InvalidOperationException($"{owner}.{key}.{rowId} has an invalid shape.");
            JsonPath.RequiredString(row, "label", owner);
            JsonPath.RequiredBoolean(row, "visible", owner);
            if (JsonPath.RequiredString(row, "placement", owner) is not ("top" or "center" or "bottom"))
                throw new InvalidOperationException($"{owner}.{key}.{rowId}.placement is invalid.");
            ComponentVariantSlotDocumentContract.Validate(JsonPath.RequiredObject(row, "rowSlot", owner), $"{owner}.{key}.{rowId}.rowSlot");
        }
    }

}
