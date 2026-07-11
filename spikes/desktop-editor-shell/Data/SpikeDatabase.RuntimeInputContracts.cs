using Microsoft.Data.Sqlite;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void NormalizeRuntimeInputContracts(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT id, component_type, design_preview_json FROM component_classes";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string Json)>();
        while (reader.Read())
        {
            var preview = ParseJsonObject(ReadString(reader, 2));
            var authoritativeInputs = ComponentInputsForComponent(reader.GetString(1));
            if (preview["inputs"] is JsonArray currentInputs
                && JsonNode.DeepEquals(currentInputs, authoritativeInputs))
            {
                continue;
            }

            preview["inputs"] = authoritativeInputs.DeepClone();
            updates.Add((reader.GetString(0), preview.ToJsonString()));
        }
        reader.Close();

        foreach (var update in updates)
        {
            Execute(
                connection,
                "UPDATE component_classes SET design_preview_json = $json WHERE id = $id",
                ("$json", update.Json),
                ("$id", update.Id));
        }

        NormalizeConversationRuntimeInputContracts(connection);
    }

    private static void NormalizeConversationRuntimeInputContracts(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT id, design_preview_json FROM modules WHERE record_class_id = 'module.core.chat'";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string Json)>();
        var defaults = DefaultConversationDesignPreviewJson();
        while (reader.Read())
        {
            var preview = ParseJsonObject(ReadString(reader, 1));
            var changed = false;
            MigrateConversationPreviewMessages(preview, defaults, ref changed);
            foreach (var property in defaults.Where((property) => property.Key is not "inputs" and not "actions" and not "collections" and not "messages"))
            {
                if (preview[property.Key] is not null) continue;
                preview[property.Key] = property.Value?.DeepClone();
                changed = true;
            }

            foreach (var contractKey in new[] { "inputs", "collections", "actions" })
            {
                if (JsonNode.DeepEquals(preview[contractKey], defaults[contractKey])) continue;
                preview[contractKey] = defaults[contractKey]?.DeepClone();
                changed = true;
            }

            if (changed)
            {
                updates.Add((reader.GetString(0), preview.ToJsonString()));
            }
        }
        reader.Close();

        foreach (var update in updates)
        {
            Execute(
                connection,
                "UPDATE modules SET design_preview_json = $json WHERE id = $id",
                ("$json", update.Json),
                ("$id", update.Id));
        }
    }

    private static void MigrateConversationPreviewMessages(JsonObject preview, JsonObject defaults, ref bool changed)
    {
        if (preview["messages"] is JsonArray existingMessages)
        {
            var defaultMessages = defaults["messages"] as JsonArray ?? new JsonArray();
            for (var index = 0; index < existingMessages.Count; index++)
            {
                if (existingMessages[index] is not JsonObject existing
                    || defaultMessages.ElementAtOrDefault(index) is not JsonObject defaultsForMessage)
                {
                    continue;
                }

                foreach (var (key, value) in defaultsForMessage)
                {
                    if (existing[key] is not null)
                    {
                        continue;
                    }
                    existing[key] = value?.DeepClone();
                    changed = true;
                }
            }
            RemoveLegacyConversationPreviewKeys(preview, ref changed);
            return;
        }

        var messages = defaults["messages"]?.DeepClone() as JsonArray ?? new JsonArray();
        var first = messages.ElementAtOrDefault(0) as JsonObject;
        var second = messages.ElementAtOrDefault(1) as JsonObject;
        var third = messages.ElementAtOrDefault(2) as JsonObject;
        CopyScalar(preview, "message1Text", first, "text");
        CopyScalar(preview, "message2Text", second, "text");
        CopyScalar(preview, "message3Text", third, "text");
        CopyScalar(preview, "message2StatusState", second, "statusState");
        CopyScalar(preview, "message2StatusText", second, "statusText");
        CopyScalar(preview, "bubbleRevealMode", second, "bubbleRevealMode");
        CopyScalar(preview, "textInputVisible", second, "textInputVisible");
        CopyScalar(preview, "keyboardVisible", second, "keyboardVisible");
        preview["messages"] = messages;
        RemoveLegacyConversationPreviewKeys(preview, ref changed);
        changed = true;
    }

    private static void RemoveLegacyConversationPreviewKeys(JsonObject preview, ref bool changed)
    {
        foreach (var key in new[]
        {
            "message1Text",
            "message2Text",
            "message3Text",
            "message2StatusState",
            "message2StatusText",
            "bubbleRevealMode",
            "textInputVisible",
            "keyboardVisible",
        })
        {
            if (preview.Remove(key))
            {
                changed = true;
            }
        }
    }

    private static void CopyScalar(JsonObject source, string sourceKey, JsonObject? target, string targetKey)
    {
        if (target is not null && source[sourceKey] is JsonNode value)
        {
            target[targetKey] = value.DeepClone();
        }
    }
}
