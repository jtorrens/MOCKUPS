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
            foreach (var property in defaults.Where((property) => property.Key is not "inputs" and not "actions"))
            {
                if (preview[property.Key] is not null) continue;
                preview[property.Key] = property.Value?.DeepClone();
                changed = true;
            }

            foreach (var contractKey in new[] { "inputs", "actions" })
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
}
