using Microsoft.Data.Sqlite;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static void NormalizePreviewActionCompletionContracts(SqliteConnection connection)
    {
        foreach (var row in QueryComponentClassRows(connection))
        {
            var preview = ParseJsonObject(row.DesignPreviewJson);
            NormalizePreviewActionCompletion(preview);
            Execute(
                connection,
                "UPDATE component_classes SET design_preview_json = $preview WHERE id = $id",
                ("$id", row.Id),
                ("$preview", preview.ToJsonString()));
        }

        foreach (var row in QueryModuleRows(connection))
        {
            var preview = ParseJsonObject(row.DesignPreviewJson);
            NormalizePreviewActionCompletion(preview);
            Execute(
                connection,
                "UPDATE modules SET design_preview_json = $preview WHERE id = $id",
                ("$id", row.Id),
                ("$preview", preview.ToJsonString()));
        }
    }

    private static void NormalizePreviewActionCompletion(JsonObject preview)
    {
        SetMissingCompletionBehavior(preview["actions"] as JsonArray);
        NormalizeFullScreenTarget(preview["actions"] as JsonArray);
        if (preview["collections"] is not JsonArray collections) return;
        foreach (var collection in collections.OfType<JsonObject>())
        {
            SetMissingCompletionBehavior(collection["itemActions"] as JsonArray);
            NormalizeFullScreenTarget(collection["itemActions"] as JsonArray);
        }
    }

    private static void NormalizeFullScreenTarget(JsonArray? actions)
    {
        if (actions is null) return;
        foreach (var action in actions.OfType<JsonObject>().Where((candidate) =>
                     candidate["id"]?.GetValue<string>() == "fullScreen"))
        {
            action.Remove("activateInputIds");
            action["targetInputId"] = "isFullScreen";
            action["targetMode"] = "toggle";
        }
    }

    private static void SetMissingCompletionBehavior(JsonArray? actions)
    {
        if (actions is null) return;
        foreach (var action in actions.OfType<JsonObject>())
        {
            action["completionBehavior"] ??= "reset";
        }
    }
}
