using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public StatusBarSettings GetStatusBarSettings(string statusBarId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, name, family, config_json, metadata_json FROM status_bars WHERE id = $id";
        command.Parameters.AddWithValue("$id", statusBarId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing status bar '{statusBarId}'.");
        }

        return new StatusBarSettings(
            reader.GetString(0),
            reader.GetString(1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4));
    }

    public string GetStatusBarFieldValue(string statusBarId, string fieldId)
    {
        var settings = GetStatusBarSettings(statusBarId);
        var config = StatusBarConfig(settings.ConfigJson);
        return fieldId switch
        {
            "statusBar.family" => settings.Family,
            "statusBar.layout.height" => JsonNumberString(config, ["layout", "height"], "54"),
            "statusBar.layout.itemSize" => JsonNumberString(config, ["layout", "itemSize"], "18"),
            "statusBar.layout.gap" => JsonNumberString(config, ["layout", "gap"], "6"),
            "statusBar.layout.sidePadding" => JsonNumberString(config, ["layout", "sidePadding"], "24"),
            _ => "",
        };
    }

    public IReadOnlyList<StatusBarItem> GetStatusBarItems(string statusBarId)
    {
        return StatusBarItems(StatusBarConfig(GetStatusBarSettings(statusBarId).ConfigJson));
    }

    public IReadOnlyList<StatusBarItem> GetStatusBarComponentItems(string componentClassId)
    {
        return StatusBarItems(StatusBarConfig(GetComponentClassSettings(componentClassId).ConfigJson));
    }

    public void UpdateStatusBarField(string statusBarId, string fieldId, string value)
    {
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            if (fieldId == "statusBar.family")
            {
                Execute(connection, "UPDATE status_bars SET family = $family WHERE id = $id", ("$id", statusBarId), ("$family", value));
                return;
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT config_json FROM status_bars WHERE id = $id";
            command.Parameters.AddWithValue("$id", statusBarId);
            var config = StatusBarConfig(command.ExecuteScalar() as string ?? "{}");
            var nextValue = NumericText.Int32(value, 0);
            switch (fieldId)
            {
                case "statusBar.layout.height":
                    SetJsonNumber(config, ["layout", "height"], nextValue);
                    break;
                case "statusBar.layout.itemSize":
                    SetJsonNumber(config, ["layout", "itemSize"], nextValue);
                    break;
                case "statusBar.layout.gap":
                    SetJsonNumber(config, ["layout", "gap"], nextValue);
                    break;
                case "statusBar.layout.sidePadding":
                    SetJsonNumber(config, ["layout", "sidePadding"], nextValue);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown status bar field '{fieldId}'.");
            }

            Execute(
                connection,
                "UPDATE status_bars SET config_json = $configJson WHERE id = $id",
                ("$id", statusBarId),
                ("$configJson", config.ToJsonString()));
        }
    }

    public void UpdateStatusBarItem(string statusBarId, int index, StatusBarItem patch)
    {
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT config_json FROM status_bars WHERE id = $id";
            command.Parameters.AddWithValue("$id", statusBarId);
            var config = StatusBarConfig(command.ExecuteScalar() as string ?? "{}");
            var items = config["items"] as JsonArray ?? new JsonArray();
            while (items.Count <= index)
            {
                items.Add(JsonSerializer.SerializeToNode(DefaultStatusBarItems().ElementAtOrDefault(items.Count) ?? DefaultStatusBarItems()[0])!);
            }

            items[index] = StatusBarItemToJson(patch);
            config["items"] = items;
            Execute(
                connection,
                "UPDATE status_bars SET config_json = $configJson WHERE id = $id",
                ("$id", statusBarId),
                ("$configJson", config.ToJsonString()));
        }
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

    public NavigationBarSettings GetNavigationBarSettings(string navigationBarId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, name, family, config_json, metadata_json FROM navigation_bars WHERE id = $id";
        command.Parameters.AddWithValue("$id", navigationBarId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing navigation bar '{navigationBarId}'.");
        }

        return new NavigationBarSettings(
            reader.GetString(0),
            reader.GetString(1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4));
    }

    public string GetNavigationBarFieldValue(string navigationBarId, string fieldId)
    {
        var settings = GetNavigationBarSettings(navigationBarId);
        var config = NavigationBarConfig(settings.ConfigJson);
        return fieldId switch
        {
            "navigationBar.family" => settings.Family,
            "navigationBar.type" => JsonString(config, ["type"]) is { Length: > 0 } type ? type : "buttons",
            "navigationBar.layout.height" => JsonNumberString(config, ["layout", "height"], "34"),
            "navigationBar.layout.itemSize" => JsonNumberString(config, ["layout", "itemSize"], "18"),
            "navigationBar.layout.sidePadding" => JsonNumberString(config, ["layout", "sidePadding"], "40"),
            "navigationBar.layout.strokeWidth" => JsonNumberString(config, ["layout", "strokeWidth"], "2"),
            "navigationBar.layout.cornerRadius" => JsonNumberString(config, ["layout", "cornerRadius"], "3"),
            "navigationBar.layout.filled" => BoolToString(JsonBool(config, ["layout", "filled"])),
            "navigationBar.gesture.width" => JsonNumberString(config, ["gesture", "width"], "134"),
            "navigationBar.gesture.height" => JsonNumberString(config, ["gesture", "height"], "5"),
            "navigationBar.gesture.cornerRadius" => JsonNumberString(config, ["gesture", "cornerRadius"], "3"),
            _ => "",
        };
    }

    public IReadOnlyList<NavigationBarItem> GetNavigationBarItems(string navigationBarId)
    {
        return NavigationBarItems(NavigationBarConfig(GetNavigationBarSettings(navigationBarId).ConfigJson));
    }

    public IReadOnlyList<NavigationBarItem> GetNavigationBarComponentItems(string componentClassId)
    {
        return NavigationBarItems(NavigationBarConfig(GetComponentClassSettings(componentClassId).ConfigJson));
    }

    public void UpdateNavigationBarField(string navigationBarId, string fieldId, string value)
    {
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            if (fieldId == "navigationBar.family")
            {
                Execute(connection, "UPDATE navigation_bars SET family = $family WHERE id = $id", ("$id", navigationBarId), ("$family", value));
                return;
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT config_json FROM navigation_bars WHERE id = $id";
            command.Parameters.AddWithValue("$id", navigationBarId);
            var config = NavigationBarConfig(command.ExecuteScalar() as string ?? "{}");
            switch (fieldId)
            {
                case "navigationBar.type":
                    var type = value is "gestureBar" ? "gestureBar" : "buttons";
                    SetJsonValue(config, ["type"], JsonValue.Create(type)!);
                    break;
                case "navigationBar.layout.height":
                    SetJsonNumber(config, ["layout", "height"], NumericText.Int32(value, 0));
                    break;
                case "navigationBar.layout.itemSize":
                    SetJsonNumber(config, ["layout", "itemSize"], NumericText.Int32(value, 0));
                    break;
                case "navigationBar.layout.sidePadding":
                    SetJsonNumber(config, ["layout", "sidePadding"], NumericText.Int32(value, 0));
                    break;
                case "navigationBar.layout.strokeWidth":
                    SetJsonValue(config, ["layout", "strokeWidth"], NumberNode(value));
                    break;
                case "navigationBar.layout.cornerRadius":
                    SetJsonNumber(config, ["layout", "cornerRadius"], NumericText.Int32(value, 0));
                    break;
                case "navigationBar.layout.filled":
                    SetJsonValue(config, ["layout", "filled"], JsonValue.Create(value.Equals("true", StringComparison.OrdinalIgnoreCase))!);
                    break;
                case "navigationBar.gesture.width":
                    SetJsonNumber(config, ["gesture", "width"], NumericText.Int32(value, 0));
                    break;
                case "navigationBar.gesture.height":
                    SetJsonNumber(config, ["gesture", "height"], NumericText.Int32(value, 0));
                    break;
                case "navigationBar.gesture.cornerRadius":
                    SetJsonNumber(config, ["gesture", "cornerRadius"], NumericText.Int32(value, 0));
                    break;
                default:
                    throw new InvalidOperationException($"Unknown navigation bar field '{fieldId}'.");
            }

            Execute(
                connection,
                "UPDATE navigation_bars SET config_json = $configJson WHERE id = $id",
                ("$id", navigationBarId),
                ("$configJson", config.ToJsonString()));
        }
    }

    public void UpdateNavigationBarItem(string navigationBarId, int index, NavigationBarItem patch)
    {
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT config_json FROM navigation_bars WHERE id = $id";
            command.Parameters.AddWithValue("$id", navigationBarId);
            var config = NavigationBarConfig(command.ExecuteScalar() as string ?? "{}");
            var items = config["items"] as JsonArray ?? new JsonArray();
            while (items.Count <= index)
            {
                items.Add(NavigationBarItemToJson(DefaultNavigationBarItems().ElementAtOrDefault(items.Count) ?? DefaultNavigationBarItems()[0]));
            }

            items[index] = NavigationBarItemToJson(patch);
            config["items"] = items;
            Execute(
                connection,
                "UPDATE navigation_bars SET config_json = $configJson WHERE id = $id",
                ("$id", navigationBarId),
                ("$configJson", config.ToJsonString()));
        }
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
            ["schemaVersion"] = 2,
            ["layout"] = new JsonObject
            {
                ["height"] = 54,
                ["itemSize"] = 18,
                ["gap"] = 6,
                ["sidePadding"] = 24,
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
        parsed["schemaVersion"] ??= 2;
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
            ["schemaVersion"] = 1,
            ["type"] = "buttons",
            ["layout"] = new JsonObject
            {
                ["height"] = 34,
                ["itemSize"] = 18,
                ["sidePadding"] = 40,
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
        parsed["schemaVersion"] ??= 1;
        parsed["type"] = JsonString(parsed, ["type"]) is "gestureBar" ? "gestureBar" : "buttons";
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
}
