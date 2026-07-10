using Microsoft.Data.Sqlite;
using System.Collections.Generic;
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
    }
}
