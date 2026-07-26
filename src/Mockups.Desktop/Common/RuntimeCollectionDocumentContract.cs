using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Common;

internal static class RuntimeCollectionDocumentContract
{
    public static void Validate(JsonArray items, string owner)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index] as JsonObject
                ?? throw new InvalidOperationException($"{owner} item at index {index} must be an object.");
            var id = JsonPath.RequiredString(item, "id", $"{owner} item at index {index}");
            if (!ids.Add(id))
            {
                throw new InvalidOperationException($"{owner} contains duplicate stable id '{id}'.");
            }
        }
    }

    public static string RequireNewItem(JsonArray items, JsonObject item, string owner)
    {
        Validate(items, owner);
        var id = JsonPath.RequiredString(item, "id", $"New {owner} item");
        foreach (var existing in items)
        {
            if (existing?["id"]?.GetValue<string>() == id)
            {
                throw new InvalidOperationException($"{owner} already contains stable id '{id}'.");
            }
        }
        return id;
    }
}
