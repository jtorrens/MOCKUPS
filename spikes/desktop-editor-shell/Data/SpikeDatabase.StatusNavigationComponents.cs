using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public IReadOnlyList<StatusBarItem> GetStatusBarComponentItems(string componentClassId)
    {
        return StatusBarItems(StatusBarConfig(GetComponentClassSettings(componentClassId).ConfigJson));
    }

    public void UpdateStatusBarComponentItem(string componentClassId, int index, StatusBarItem patch)
    {
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            if (!settings.ComponentType.Equals("status_bar", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Component class '{componentClassId}' is not a status bar.");
            }

            var config = StatusBarConfig(settings.ConfigJson);
            var items = config["items"] as JsonArray ?? new JsonArray();
            while (items.Count <= index)
            {
                items.Add(JsonSerializer.SerializeToNode(DefaultStatusBarItems().ElementAtOrDefault(items.Count) ?? DefaultStatusBarItems()[0])!);
            }

            items[index] = StatusBarItemToJson(patch);
            config["items"] = items;
            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson WHERE id = $id",
                ("$id", componentClassId),
                ("$configJson", config.ToJsonString()));
        }
    }

    public IReadOnlyList<NavigationBarItem> GetNavigationBarComponentItems(string componentClassId)
    {
        return NavigationBarItems(NavigationBarConfig(GetComponentClassSettings(componentClassId).ConfigJson));
    }

    public void UpdateNavigationBarComponentItem(string componentClassId, int index, NavigationBarItem patch)
    {
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var settings = GetComponentClassSettings(connection, componentClassId);
            if (!settings.ComponentType.Equals("navigation_bar", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Component class '{componentClassId}' is not a navigation bar.");
            }

            var config = NavigationBarConfig(settings.ConfigJson);
            var items = config["items"] as JsonArray ?? new JsonArray();
            while (items.Count <= index)
            {
                items.Add(NavigationBarItemToJson(DefaultNavigationBarItems().ElementAtOrDefault(items.Count) ?? DefaultNavigationBarItems()[0]));
            }

            items[index] = NavigationBarItemToJson(patch);
            config["items"] = items;
            Execute(
                connection,
                "UPDATE component_classes SET config_json = $configJson WHERE id = $id",
                ("$id", componentClassId),
                ("$configJson", config.ToJsonString()));
        }
    }

    private static string DefaultStatusBarConfigJson()
    {
        var config = new JsonObject
        {
            ["schemaVersion"] = 3,
            ["foregroundColorToken"] = "theme.icons.primary",
            ["backgroundColorToken"] = "theme.colors.background",
            ["backgroundAlpha"] = 1,
            ["layout"] = new JsonObject
            {
                ["height"] = 54,
                ["itemSize"] = 18,
                ["gap"] = "theme.spacing.m",
                ["sidePadding"] = "theme.spacing.xxl",
            },
            ["items"] = new JsonArray(DefaultStatusBarItems().Select(StatusBarItemToJson).ToArray<JsonNode?>()),
        };
        return config.ToJsonString();
    }

    private static List<StatusBarItem> DefaultStatusBarItems()
    {
        return
        [
            new("time", "Time", "text", "9:41", "", false, "left", 10),
            new("carrier", "Carrier", "text", "", "", false, "off", 20),
            new("signal", "Signal", "generatedSignal", "4", "", false, "right", 10),
            new("wifi", "Wi‑Fi", "iconToken", "", "status_wifi", false, "right", 20),
            new("soundOff", "Sound Off", "iconToken", "", "media_volume_off", false, "off", 30),
            new("bluetooth", "Bluetooth", "iconToken", "", "status_bluetooth", false, "off", 40),
            new("battery", "Battery", "generatedBattery", "85", "", false, "right", 50),
        ];
    }

    private static JsonObject StatusBarConfig(string json)
    {
        var fallback = ParseJsonObject(DefaultStatusBarConfigJson());
        var parsed = ParseJsonObject(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var layout = parsed["layout"] as JsonObject ?? [];
        var fallbackLayout = fallback["layout"]!.AsObject();
        parsed["schemaVersion"] = 3;
        parsed["foregroundColorToken"] ??= fallback["foregroundColorToken"]!.DeepClone();
        parsed["backgroundColorToken"] ??= fallback["backgroundColorToken"]!.DeepClone();
        parsed["backgroundAlpha"] ??= fallback["backgroundAlpha"]!.DeepClone();
        parsed["layout"] = new JsonObject
        {
            ["height"] = GetJsonValue(layout, ["height"])?.DeepClone() ?? fallbackLayout["height"]!.DeepClone(),
            ["itemSize"] = GetJsonValue(layout, ["itemSize"])?.DeepClone() ?? fallbackLayout["itemSize"]!.DeepClone(),
            ["gap"] = GetJsonValue(layout, ["gap"])?.DeepClone() ?? fallbackLayout["gap"]!.DeepClone(),
            ["sidePadding"] = GetJsonValue(layout, ["sidePadding"])?.DeepClone() ?? fallbackLayout["sidePadding"]!.DeepClone(),
        };
        if (parsed["items"] is not JsonArray)
        {
            parsed["items"] = new JsonArray(DefaultStatusBarItems().Select(StatusBarItemToJson).ToArray<JsonNode?>());
        }

        return parsed;
    }

    private static IReadOnlyList<StatusBarItem> StatusBarItems(JsonObject config)
    {
        var defaults = DefaultStatusBarItems();
        var rawItems = config["items"] as JsonArray ?? [];
        return rawItems.Select((raw, index) =>
        {
            var item = raw as JsonObject ?? [];
            var fallback = defaults.ElementAtOrDefault(index) ?? defaults[0];
            var kind = JsonString(item, ["kind"]);
            if (kind is not ("text" or "iconToken" or "generatedBattery" or "generatedSignal"))
            {
                kind = fallback.Kind;
            }

            var zone = JsonString(item, ["zone"]);
            if (zone is not ("off" or "left" or "right"))
            {
                zone = fallback.Zone;
            }

            return new StatusBarItem(
                JsonString(item, ["id"]) is { Length: > 0 } id ? id : fallback.Id,
                JsonString(item, ["label"]) is { Length: > 0 } label ? label : fallback.Label,
                kind,
                JsonString(item, ["value"]) is { Length: > 0 } stringValue
                    ? stringValue
                    : JsonNumberString(item, ["value"]),
                JsonString(item, ["token"]) is { Length: > 0 } token ? token : fallback.Token,
                JsonBool(item, ["charging"]),
                zone,
                NumericText.Int32(JsonNumberString(item, ["order"]), fallback.Order));
        }).ToList();
    }

    private static JsonObject StatusBarItemToJson(StatusBarItem item)
    {
        var json = new JsonObject
        {
            ["id"] = item.Id,
            ["label"] = item.Label,
            ["kind"] = item.Kind,
            ["zone"] = item.Zone,
            ["order"] = item.Order,
        };
        if (item.Kind == "iconToken")
        {
            json["token"] = item.Token;
        }
        else if (item.Kind == "generatedBattery" || item.Kind == "generatedSignal")
        {
            json["value"] = NumericText.Int32(item.Value, 0);
            if (item.Kind == "generatedBattery")
            {
                json["charging"] = item.Charging;
            }
        }
        else
        {
            json["value"] = item.Value;
        }

        return json;
    }

    private static int StatusBarItemCount(string configJson)
    {
        return StatusBarItems(StatusBarConfig(configJson)).Count;
    }

    private static string DefaultNavigationBarConfigJson()
    {
        var config = new JsonObject
        {
            ["schemaVersion"] = 2,
            ["type"] = "buttons",
            ["foregroundColorToken"] = "theme.icons.primary",
            ["backgroundColorToken"] = "theme.colors.background",
            ["backgroundAlpha"] = 1,
            ["layout"] = new JsonObject
            {
                ["height"] = 34,
                ["itemSize"] = 18,
                ["sidePadding"] = "theme.spacing.xxl",
                ["strokeWidth"] = 2,
                ["cornerRadius"] = 3,
                ["filled"] = false,
            },
            ["gesture"] = new JsonObject
            {
                ["width"] = 134,
                ["height"] = 5,
                ["cornerRadius"] = 3,
            },
            ["items"] = new JsonArray(DefaultNavigationBarItems().Select(NavigationBarItemToJson).ToArray<JsonNode?>()),
        };
        return config.ToJsonString();
    }

    private static List<NavigationBarItem> DefaultNavigationBarItems()
    {
        return
        [
            new("back", "Back", "generatedBack", "left", 10),
            new("home", "Home", "generatedHome", "center", 10),
            new("recents", "Recents", "generatedRecents", "right", 10),
        ];
    }

    private static JsonObject NavigationBarConfig(string json)
    {
        var fallback = ParseJsonObject(DefaultNavigationBarConfigJson());
        var parsed = ParseJsonObject(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var layout = parsed["layout"] as JsonObject ?? [];
        var gesture = parsed["gesture"] as JsonObject ?? [];
        var fallbackLayout = fallback["layout"]!.AsObject();
        var fallbackGesture = fallback["gesture"]!.AsObject();
        parsed["schemaVersion"] = 2;
        parsed["type"] = JsonString(parsed, ["type"]) is "gestureBar" ? "gestureBar" : "buttons";
        parsed["foregroundColorToken"] ??= fallback["foregroundColorToken"]!.DeepClone();
        parsed["backgroundColorToken"] ??= fallback["backgroundColorToken"]!.DeepClone();
        parsed["backgroundAlpha"] ??= fallback["backgroundAlpha"]!.DeepClone();
        parsed["layout"] = new JsonObject
        {
            ["height"] = GetJsonValue(layout, ["height"])?.DeepClone() ?? fallbackLayout["height"]!.DeepClone(),
            ["itemSize"] = GetJsonValue(layout, ["itemSize"])?.DeepClone() ?? fallbackLayout["itemSize"]!.DeepClone(),
            ["sidePadding"] = GetJsonValue(layout, ["sidePadding"])?.DeepClone() ?? fallbackLayout["sidePadding"]!.DeepClone(),
            ["strokeWidth"] = GetJsonValue(layout, ["strokeWidth"])?.DeepClone() ?? fallbackLayout["strokeWidth"]!.DeepClone(),
            ["cornerRadius"] = GetJsonValue(layout, ["cornerRadius"])?.DeepClone() ?? fallbackLayout["cornerRadius"]!.DeepClone(),
            ["filled"] = GetJsonValue(layout, ["filled"])?.DeepClone() ?? fallbackLayout["filled"]!.DeepClone(),
        };
        parsed["gesture"] = new JsonObject
        {
            ["width"] = GetJsonValue(gesture, ["width"])?.DeepClone() ?? fallbackGesture["width"]!.DeepClone(),
            ["height"] = GetJsonValue(gesture, ["height"])?.DeepClone() ?? fallbackGesture["height"]!.DeepClone(),
            ["cornerRadius"] = GetJsonValue(gesture, ["cornerRadius"])?.DeepClone() ?? fallbackGesture["cornerRadius"]!.DeepClone(),
        };
        if (parsed["items"] is not JsonArray)
        {
            parsed["items"] = new JsonArray(DefaultNavigationBarItems().Select(NavigationBarItemToJson).ToArray<JsonNode?>());
        }

        return parsed;
    }

    private static IReadOnlyList<NavigationBarItem> NavigationBarItems(JsonObject config)
    {
        var defaults = DefaultNavigationBarItems();
        var rawItems = config["items"] as JsonArray ?? [];
        return rawItems.Select((raw, index) =>
        {
            var item = raw as JsonObject ?? [];
            var fallback = defaults.ElementAtOrDefault(index) ?? defaults[0];
            var kind = JsonString(item, ["kind"]);
            if (kind is not ("generatedBack" or "generatedHome" or "generatedRecents"))
            {
                kind = fallback.Kind;
            }

            var zone = JsonString(item, ["zone"]);
            if (zone is not ("off" or "left" or "center" or "right"))
            {
                zone = fallback.Zone;
            }

            return new NavigationBarItem(
                JsonString(item, ["id"]) is { Length: > 0 } id ? id : fallback.Id,
                JsonString(item, ["label"]) is { Length: > 0 } label ? label : fallback.Label,
                kind,
                zone,
                NumericText.Int32(JsonNumberString(item, ["order"]), fallback.Order));
        }).ToList();
    }

    private static JsonObject NavigationBarItemToJson(NavigationBarItem item)
    {
        return new JsonObject
        {
            ["id"] = item.Id,
            ["label"] = item.Label,
            ["kind"] = item.Kind,
            ["zone"] = item.Zone,
            ["order"] = item.Order,
        };
    }

    private static int NavigationBarItemCount(string configJson)
    {
        return NavigationBarItems(NavigationBarConfig(configJson)).Count;
    }
    public IReadOnlyList<FieldOption> GetStatusBarComponentPresetOptions(string projectId)
    {
        return GetComponentPresetReferenceOptionsByType(projectId, "status_bar", includeNone: true);
    }

    public IReadOnlyList<FieldOption> GetNavigationBarComponentPresetOptions(string projectId)
    {
        return GetComponentPresetReferenceOptionsByType(projectId, "navigation_bar", includeNone: true);
    }

}
