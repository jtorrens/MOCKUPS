using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static readonly IReadOnlyDictionary<string, string> RetiredRadiusTokens =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["theme.radii.control"] = "theme.radii.m",
            ["theme.radii.card"] = "theme.radii.xl",
            ["theme.radii.panel"] = "theme.radii.xxl",
            ["theme.radii.surface"] = "theme.radii.xl",
            ["theme.radii.pill"] = "theme.radii.full",
            ["theme.radii.avatar"] = "theme.radii.full",
        };

    private static readonly IReadOnlyDictionary<string, int> CanonicalRadiusValues =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["none"] = 0,
            ["xs"] = 2,
            ["s"] = 4,
            ["m"] = 8,
            ["l"] = 12,
            ["xl"] = 16,
            ["xxl"] = 24,
            ["full"] = 999,
        };

    private static void NormalizeRadiusTokenVocabulary(SqliteConnection connection)
    {
        foreach (var theme in QueryThemeRows(connection))
        {
            var tokens = ParseJsonObject(theme.TokensJson);
            var radii = tokens["radii"] as JsonObject ?? new JsonObject();
            tokens["radii"] = radii;
            var changed = false;
            foreach (var (name, value) in CanonicalRadiusValues)
            {
                if (radii[name] is JsonValue current
                    && current.TryGetValue<int>(out var currentValue)
                    && currentValue == value)
                {
                    continue;
                }
                radii[name] = value;
                changed = true;
            }

            foreach (var name in new[] { "control", "card", "panel", "surface", "pill", "avatar" })
            {
                changed |= radii.Remove(name);
            }

            if (changed)
            {
                Execute(
                    connection,
                    "UPDATE themes SET tokens_json = $tokensJson WHERE id = $id",
                    ("$id", theme.Id),
                    ("$tokensJson", tokens.ToJsonString()));
            }
        }

        foreach (var row in QueryComponentClassRows(connection))
        {
            var config = ParseJsonObject(row.ConfigJson);
            var metadata = ParseJsonObject(row.MetadataJson);
            var changed = ReplaceRadiusTokenReferences(config);
            if (metadata["presets"] is JsonArray presets)
            {
                foreach (var preset in presets.OfType<JsonObject>())
                {
                    if (preset["config"] is JsonObject presetConfig)
                    {
                        changed |= ReplaceRadiusTokenReferences(presetConfig);
                    }
                }
            }

            if (changed)
            {
                Execute(
                    connection,
                    "UPDATE component_classes SET config_json = $configJson, metadata_json = $metadataJson WHERE id = $id",
                    ("$id", row.Id),
                    ("$configJson", config.ToJsonString()),
                    ("$metadataJson", metadata.ToJsonString()));
            }
        }
    }

    private static bool ReplaceRadiusTokenReferences(JsonNode node)
    {
        var changed = false;
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, child) in obj.ToList())
                {
                    if (child is JsonValue value
                        && value.TryGetValue<string>(out var token)
                        && RetiredRadiusTokens.TryGetValue(token, out var replacement))
                    {
                        obj[key] = replacement;
                        changed = true;
                    }
                    else if (child is not null)
                    {
                        changed |= ReplaceRadiusTokenReferences(child);
                    }
                }
                break;
            case JsonArray array:
                foreach (var child in array.Where((child) => child is not null))
                {
                    changed |= ReplaceRadiusTokenReferences(child!);
                }
                break;
        }
        return changed;
    }

    private static void NormalizeModuleAppearanceModes(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT id, config_json FROM modules";
        using var reader = select.ExecuteReader();
        var updates = new List<(string Id, string ConfigJson)>();
        while (reader.Read())
        {
            var config = ParseJsonObject(ReadString(reader, 1));
            var mode = config["appearanceMode"] is JsonValue modeValue
                && modeValue.TryGetValue<string>(out var parsedMode)
                    ? parsedMode
                    : "";
            if (mode is "inherit" or "light" or "dark") continue;
            config["appearanceMode"] = "inherit";
            updates.Add((reader.GetString(0), config.ToJsonString()));
        }
        reader.Close();

        foreach (var (id, configJson) in updates)
        {
            Execute(
                connection,
                "UPDATE modules SET config_json = $configJson WHERE id = $id",
                ("$id", id),
                ("$configJson", configJson));
        }
    }
}
