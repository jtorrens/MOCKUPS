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
        NormalizeStateActions(preview);
        if (preview["collections"] is not JsonArray collections) return;
        foreach (var collection in collections.OfType<JsonObject>())
        {
            SetMissingCompletionBehavior(collection["itemActions"] as JsonArray);
            NormalizeFullScreenTarget(collection["itemActions"] as JsonArray);
            NormalizeCollectionActions(preview, collection);
        }
    }

    private static void NormalizeStateActions(JsonObject preview)
    {
        var componentType = preview["componentType"]?.GetValue<string>() ?? "";
        if (componentType == "notification")
        {
            EnsureReflowTargetAction(
                preview,
                "changeDisplayMode",
                "Display mode",
                "displayMode",
                "displayModeTransition",
                "displayModeElapsedMs",
                "displayModeFrom");
            return;
        }
        if (componentType is "collectionStack" or "notifications")
        {
            EnsureReflowTargetAction(
                preview,
                "changeDistribution",
                "Distribution",
                "distributionMode",
                "distributionTransition",
                "distributionElapsedMs",
                "distributionFrom");
            return;
        }
        if (componentType == "keypad")
        {
            preview["pushTrigger"] ??= false;
            preview["pushElapsedMs"] ??= 0;
            var actions = preview["actions"] as JsonArray ?? new JsonArray();
            preview["actions"] = actions;
            if (!actions.OfType<JsonObject>().Any((candidate) => candidate["id"]?.GetValue<string>() == "pushKey"))
            {
                actions.Add(new JsonObject
                {
                    ["id"] = "pushKey",
                    ["label"] = "Push key",
                    ["playInputId"] = "pushTrigger",
                    ["targetInputId"] = "pushedKey",
                    ["targetMode"] = "option",
                    ["targetOptions"] = KeypadActionOptions(),
                    ["durationThemeToken"] = "theme.motion.buttonPushedDurationMs",
                    ["timeJsonKey"] = "pushElapsedMs",
                    ["timeUnit"] = "milliseconds",
                    ["prewarmFrames"] = false,
                    ["completionBehavior"] = "holdFinal",
                });
            }
        }
    }

    private static void NormalizeCollectionActions(JsonObject preview, JsonObject collection)
    {
        var componentType = preview["componentType"]?.GetValue<string>() ?? "";
        var collectionId = collection["id"]?.GetValue<string>() ?? "";
        var actions = collection["itemActions"] as JsonArray ?? new JsonArray();
        collection["itemActions"] = actions;
        if (componentType is "collectionStack" or "notifications"
            && !actions.OfType<JsonObject>().Any((candidate) => candidate["id"]?.GetValue<string>() == "togglePresent"))
        {
            actions.Add(new JsonObject
            {
                ["id"] = "togglePresent",
                ["label"] = "Presence",
                ["playInputId"] = "presenceTransition",
                ["targetInputId"] = "present",
                ["targetMode"] = "toggle",
                ["durationSeconds"] = 0.3,
                ["timeJsonKey"] = "presenceElapsedMs",
                ["timeUnit"] = "milliseconds",
                ["prewarmFrames"] = false,
                ["completionBehavior"] = "reset",
            });
        }
        if (collectionId == "messages"
            && !actions.OfType<JsonObject>().Any((candidate) => candidate["id"]?.GetValue<string>() == "fullScreen"))
        {
            actions.Add(new JsonObject
            {
                ["id"] = "fullScreen",
                ["label"] = "Full screen",
                ["playInputId"] = "fullScreenTransition",
                ["targetInputId"] = "isFullScreen",
                ["targetMode"] = "toggle",
                ["durationSeconds"] = 0.3,
                ["timeJsonKey"] = "motionElapsedMs",
                ["timeUnit"] = "milliseconds",
                ["prewarmFrames"] = false,
                ["visibleWhenItemJsonKey"] = "mediaType",
                ["visibleWhenItemValues"] = new JsonArray("video"),
                ["completionBehavior"] = "reset",
            });
        }
    }

    private static JsonArray KeypadActionOptions()
    {
        return new JsonArray(new[] { "1", "2", "3", "4", "5", "6", "7", "8", "9", "*", "0", "#" }
            .Select((value) => (JsonNode?)new JsonObject { ["value"] = value, ["label"] = value })
            .ToArray());
    }

    private static void EnsureReflowTargetAction(
        JsonObject preview,
        string id,
        string label,
        string targetInputId,
        string playInputId,
        string timeJsonKey,
        string targetFromJsonKey)
    {
        preview[playInputId] ??= false;
        preview[timeJsonKey] ??= 0;
        preview[targetFromJsonKey] ??= preview[targetInputId]?.DeepClone();
        var actions = preview["actions"] as JsonArray ?? new JsonArray();
        preview["actions"] = actions;
        var action = actions.OfType<JsonObject>().FirstOrDefault((candidate) =>
            candidate["id"]?.GetValue<string>() == id);
        if (action is null)
        {
            action = new JsonObject();
            actions.Add(action);
        }
        action["id"] = id;
        action["label"] = label;
        action["playInputId"] = playInputId;
        action["targetInputId"] = targetInputId;
        action["targetMode"] = "option";
        action["targetFromJsonKey"] = targetFromJsonKey;
        action["durationThemeToken"] = "theme.motion.reflowDurationMs";
        action["timeJsonKey"] = timeJsonKey;
        action["timeUnit"] = "milliseconds";
        action["prewarmFrames"] = false;
        action["completionBehavior"] = "reset";
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
