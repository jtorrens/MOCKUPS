using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void NormalizeRuntimeInputContracts(SqliteConnection connection)
    {
        RemovePersistedDesignPreviewTestValues(connection);
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

    private static void NormalizeModuleInstanceRuntimePayloads(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT mi.id, mi.content_json, m.design_preview_json FROM module_instances mi JOIN modules m ON m.id = mi.module_id";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string Json)>();
        while (reader.Read())
        {
            var original = ReadString(reader, 1);
            var content = ParseJsonObject(original);
            var contract = ParseJsonObject(ReadString(reader, 2));
            foreach (var input in (contract["inputs"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                if (!RuntimeInputDefinition(input)) continue;
                var jsonKey = input["jsonKey"]?.GetValue<string>() ?? "";
                if (string.IsNullOrWhiteSpace(jsonKey) || content[jsonKey] is not null) continue;
                content[jsonKey] = RuntimeDefaultValue(input);
            }

            foreach (var collection in (contract["collections"] as JsonArray)?.OfType<JsonObject>() ?? [])
            {
                var sourceKey = collection["sourceCollectionJsonKey"]?.GetValue<string>()
                    ?? collection["jsonKey"]?.GetValue<string>()
                    ?? "";
                if (string.IsNullOrWhiteSpace(sourceKey)) continue;
                var items = content[sourceKey] as JsonArray ?? new JsonArray();
                content[sourceKey] = items;
                foreach (var (item, index) in items.OfType<JsonObject>().Select((item, index) => (item, index)))
                {
                    item["id"] ??= $"{sourceKey}_{index + 1:000}";
                    foreach (var field in (collection["fields"] as JsonArray)?.OfType<JsonObject>() ?? [])
                    {
                        if (!RuntimeInputDefinition(field)) continue;
                        var jsonKey = field["jsonKey"]?.GetValue<string>() ?? "";
                        if (string.IsNullOrWhiteSpace(jsonKey) || item[jsonKey] is not null) continue;
                        item[jsonKey] = RuntimeDefaultValue(field);
                    }
                }
            }

            var next = content.ToJsonString();
            if (next != original) updates.Add((reader.GetString(0), next));
        }
        reader.Close();

        foreach (var update in updates)
        {
            Execute(connection, "UPDATE module_instances SET content_json = $json WHERE id = $id",
                ("$json", update.Json), ("$id", update.Id));
        }
    }

    private static bool RuntimeInputDefinition(JsonObject definition)
    {
        var source = definition["source"]?.GetValue<string>() ?? "runtime";
        return source == "runtime";
    }

    private static JsonNode RuntimeDefaultValue(JsonObject definition)
    {
        var value = definition["defaultValue"]?.GetValue<string>() ?? "";
        return (definition["kind"]?.GetValue<string>() ?? "text") switch
        {
            "boolean" => JsonValue.Create(bool.TryParse(value, out var boolean) && boolean)!,
            "number" when decimal.TryParse(value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                => JsonValue.Create(number)!,
            "iconList" => JsonNode.Parse(string.IsNullOrWhiteSpace(value) ? "[]" : value) ?? new JsonArray(),
            _ => JsonValue.Create(value)!,
        };
    }

    private static void RemovePersistedDesignPreviewTestValues(SqliteConnection connection)
    {
        foreach (var table in new[] { "component_classes", "modules" })
        {
            using var select = connection.CreateCommand();
            select.CommandText = $"SELECT id, design_preview_json FROM {table}";
            using var reader = select.ExecuteReader();
            var updates = new List<(string Id, string Json)>();
            while (reader.Read())
            {
                var preview = ParseJsonObject(ReadString(reader, 1));
                if (!preview.Remove("testValues"))
                {
                    continue;
                }
                updates.Add((reader.GetString(0), preview.ToJsonString()));
            }
            reader.Close();

            foreach (var update in updates)
            {
                Execute(
                    connection,
                    $"UPDATE {table} SET design_preview_json = $json WHERE id = $id",
                    ("$json", update.Json),
                    ("$id", update.Id));
            }
        }
    }

    private static void NormalizeConversationRuntimeInputContracts(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT m.id, m.design_preview_json, a.project_id FROM modules m JOIN apps a ON a.id = m.app_id WHERE m.record_class_id = 'module.core.chat'";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string Json)>();
        var defaults = DefaultConversationDesignPreviewJson();
        while (reader.Read())
        {
            var preview = ParseJsonObject(ReadString(reader, 1));
            var projectId = reader.GetString(2);
            var changed = false;
            changed |= preview.Remove("headerTitle");
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

            var buttonClassId = ScalarString(connection, "SELECT id FROM component_classes WHERE project_id = $projectId AND component_type = 'button'", ("$projectId", projectId))!;
            var beforeButtonReferences = preview.ToJsonString();
            NormalizeIconRowNodes(preview, $"{buttonClassId}::preset::{DefaultComponentPresetId}");
            changed |= !string.Equals(beforeButtonReferences, preview.ToJsonString(), System.StringComparison.Ordinal);

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

    private static void NormalizeConversationHeaderComposition(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT m.id, m.config_json, m.design_preview_json, a.project_id FROM modules m JOIN apps a ON a.id = m.app_id WHERE m.record_class_id = 'module.core.chat'";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string ConfigJson, string PreviewJson)>();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var originalConfig = ReadString(reader, 1);
            var originalPreview = ReadString(reader, 2);
            var config = ParseJsonObject(originalConfig);
            var preview = ParseJsonObject(originalPreview);
            var projectId = reader.GetString(3);
            var conversation = config["conversation"] as JsonObject ?? new JsonObject();
            config["conversation"] = conversation;
            conversation.Remove("statusBarVariant");
            conversation.Remove("navigationBarVariant");
            conversation["headerAvatarAlignment"] ??= "left";
            NormalizeConversationHeaderIconRowSlot(conversation, "headerLeftIconRowSlot", "headerLeftIconRowVariant", projectId);
            NormalizeConversationHeaderIconRowSlot(conversation, "headerRightIconRowSlot", "headerRightIconRowVariant", projectId);
            conversation["headerLeftIconRowInputs"] ??= HeaderIconRowInputs(projectId, []);
            conversation["headerRightIconRowInputs"] ??= HeaderIconRowInputs(projectId, ["media_camera"]);
            var currentAvatar = conversation["headerAvatarVariant"]?.GetValue<string>() ?? "";
            if (currentAvatar.EndsWith("::preset::default", System.StringComparison.Ordinal))
            {
                var avatarClassId = ScalarString(connection, "SELECT id FROM component_classes WHERE project_id = $projectId AND component_type = 'avatar'", ("$projectId", projectId));
                if (!string.IsNullOrWhiteSpace(avatarClassId))
                {
                    var metadata = ParseJsonObject(ScalarString(connection, "SELECT metadata_json FROM component_classes WHERE id = $id", ("$id", avatarClassId)) ?? "{}");
                    if ((metadata["presets"] as JsonArray)?.OfType<JsonObject>().Any((preset) => preset["id"]?.GetValue<string>() == "avatar_chat_header") == true)
                    {
                        conversation["headerAvatarVariant"] = $"{avatarClassId}::preset::avatar_chat_header";
                    }
                }
            }
            preview.Remove("headerLeftButtons");
            preview.Remove("headerRightButtons");
            if (preview["collections"] is JsonArray collections)
            {
                foreach (var retired in collections.OfType<JsonObject>()
                    .Where((collection) => collection["jsonKey"]?.GetValue<string>() is "headerLeftButtons" or "headerRightButtons")
                    .ToList())
                {
                    collections.Remove(retired);
                }
            }
            var configJson = config.ToJsonString();
            var previewJson = preview.ToJsonString();
            if (configJson != originalConfig || previewJson != originalPreview)
            {
                updates.Add((id, configJson, previewJson));
            }
        }
        reader.Close();
        foreach (var update in updates)
        {
            Execute(connection, "UPDATE modules SET config_json = $configJson, design_preview_json = $previewJson WHERE id = $id",
                ("$id", update.Id), ("$configJson", update.ConfigJson), ("$previewJson", update.PreviewJson));
        }
    }

    private static void NormalizeConversationHeaderIconRowSlot(JsonObject conversation, string slotKey, string retiredKey, string projectId)
    {
        var presetId = conversation[retiredKey]?.GetValue<string>();
        conversation[slotKey] ??= new JsonObject
        {
            ["presetId"] = string.IsNullOrWhiteSpace(presetId) ? SeededComponentPresetReference(projectId, "iconRow") : presetId,
            ["overrides"] = new JsonObject(),
        };
        conversation.Remove(retiredKey);
    }

    private static void MigrateConversationPreviewMessages(JsonObject preview, JsonObject defaults, ref bool changed)
    {
        if (preview["messages"] is JsonArray existingMessages)
        {
            var defaultMessages = defaults["messages"] as JsonArray ?? new JsonArray();
            for (var index = 0; index < existingMessages.Count; index++)
            {
                if (existingMessages[index] is not JsonObject existing
                    || (defaultMessages.ElementAtOrDefault(index) as JsonObject
                        ?? defaultMessages.ElementAtOrDefault(0) as JsonObject) is not JsonObject defaultsForMessage)
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
            NormalizeConversationTiming(preview, defaults, existingMessages, ref changed);
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
        preview["messages"] = messages;
        NormalizeConversationTiming(preview, defaults, messages, ref changed);
        RemoveLegacyConversationPreviewKeys(preview, ref changed);
        changed = true;
    }

    private static void NormalizeConversationTiming(
        JsonObject preview,
        JsonObject defaults,
        JsonArray messages,
        ref bool changed)
    {
        foreach (var (message, index) in messages.OfType<JsonObject>().Select((message, index) => (message, index)))
        {
            if (message["writeOnDurationFrames"] is null)
            {
                var defaultMessage = (defaults["messages"] as JsonArray)?.ElementAtOrDefault(index) as JsonObject;
                if (message["textReveal"]?["durationFrames"] is JsonNode duration)
                {
                    message["writeOnDurationFrames"] = duration.DeepClone();
                }
                else if (defaultMessage?["writeOnDurationFrames"] is JsonNode defaultWriteOn)
                {
                    message["writeOnDurationFrames"] = defaultWriteOn.DeepClone();
                }
                else
                {
                    message["writeOnDurationFrames"] = 0;
                }
                changed = true;
            }
            if (message["postWriteOnHoldFrames"] is null)
            {
                var defaultMessage = (defaults["messages"] as JsonArray)?.ElementAtOrDefault(index) as JsonObject;
                message["postWriteOnHoldFrames"] = defaultMessage?["postWriteOnHoldFrames"]?.DeepClone() ?? 0;
                changed = true;
            }
            PromoteMessageValue(preview, message, "bubbleRevealMode", ref changed);
            PromoteMessageValue(preview, message, "textInputVisible", ref changed);
            PromoteMessageValue(preview, message, "keyboardVisible", ref changed);
            RemoveMessageTimingKey(message, "bubbleRevealMode", ref changed);
            RemoveMessageTimingKey(message, "textInputVisible", ref changed);
            RemoveMessageTimingKey(message, "keyboardVisible", ref changed);
            if (message.Remove("textReveal"))
            {
                changed = true;
            }
        }

        foreach (var key in new[]
        {
            "bubbleRevealMode",
            "incomingRevealMode",
            "textInputVisible",
            "keyboardVisible",
            "typingIndicatorText",
            "typingIndicatorSizeToken",
            "typingIndicatorAnimation",
        })
        {
            if (preview[key] is null && defaults[key] is JsonNode defaultValue)
            {
                preview[key] = defaultValue.DeepClone();
                changed = true;
            }
        }
    }

    private static void PromoteMessageValue(JsonObject preview, JsonObject message, string key, ref bool changed)
    {
        if (preview[key] is null && message[key] is JsonNode value)
        {
            preview[key] = value.DeepClone();
            changed = true;
        }
    }

    private static void RemoveMessageTimingKey(JsonObject message, string key, ref bool changed)
    {
        if (message.Remove(key))
        {
            changed = true;
        }
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
            "writeOnDurationFrames",
            "postWriteOnHoldFrames",
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
