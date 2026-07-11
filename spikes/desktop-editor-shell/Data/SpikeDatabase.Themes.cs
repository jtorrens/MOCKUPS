using Microsoft.Data.Sqlite;
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
    private static readonly Dictionary<string, (string[] Light, string[] Dark)> ThemeColorPairPaths =
        ThemeColorTokenCatalog.ColorTokens.ToDictionary(
            (token) => token.Id,
            (token) => (token.LightPath.ToArray(), token.DarkPath.ToArray()),
            StringComparer.Ordinal);

    private static readonly Dictionary<string, (string[] Light, string[] Dark)> ThemeAlphaPairPaths =
        ThemeColorTokenCatalog.ColorTokens
            .Where((token) => token.HasAlpha)
            .ToDictionary(
                (token) => token.Id,
                (token) => (token.LightAlphaPath!.ToArray(), token.DarkAlphaPath!.ToArray()),
                StringComparer.Ordinal);

    private static readonly Dictionary<string, string[]> ThemeMotionTimingPaths =
        new[] { "fade", "slide", "swipe", "scale" }
            .ToDictionary(
                (transition) => $"theme.motion.{transition}",
                (transition) => new[] { "motion", "transitions", transition },
                StringComparer.Ordinal);

    private static readonly Dictionary<string, string[]> ThemeMotionEasingPaths =
        new[] { "fade", "slide", "swipe", "scale" }
            .ToDictionary(
                (transition) => $"theme.motion.{transition}.easing",
                (transition) => new[] { "motion", "transitions", transition, "easing" },
                StringComparer.Ordinal);

    public IReadOnlyList<FieldOption> GetThemeOptions(string projectId)
    {
        using var connection = OpenConnection();
        return QueryThemeRows(connection)
            .Where((theme) => theme.ProjectId == projectId)
            .OrderBy((theme) => theme.Name)
            .Select((theme) => new FieldOption(theme.Id, theme.Name))
            .ToList();
    }

    public IReadOnlyList<ThemeTokenOption> GetThemeTokenOptions(string projectId, string themeId)
    {
        using var connection = OpenConnection();
        var theme = QueryThemeRows(connection)
            .Where((row) => row.ProjectId == projectId)
            .FirstOrDefault((row) => row.Id == themeId)
            ?? QueryThemeRows(connection).FirstOrDefault((row) => row.ProjectId == projectId)
            ?? throw new InvalidOperationException($"No themes available for project '{projectId}'.");
        var tokens = ParseJsonObject(string.IsNullOrWhiteSpace(theme.TokensJson) ? "{}" : theme.TokensJson);
        var palette = QueryPaletteColorRows(connection)
            .Where((color) => color.ProjectId == projectId)
            .ToDictionary((color) => color.Token, (color) => color.ValueHex, StringComparer.Ordinal);

        var options = new List<ThemeTokenOption>();
        foreach (var pair in ThemeColorPairPaths.OrderBy((pair) => pair.Key, StringComparer.Ordinal))
        {
            var lightToken = JsonString(tokens, pair.Value.Light);
            var darkToken = JsonString(tokens, pair.Value.Dark);
            options.Add(new ThemeTokenOption(
                pair.Key,
                pair.Key.Replace("theme.", "", StringComparison.Ordinal),
                "color",
                $"{lightToken} / {darkToken}",
                PaletteHex(palette, lightToken),
                PaletteHex(palette, darkToken)));
        }

        foreach (var option in NumericThemeTokenOptions(tokens))
        {
            options.Add(option);
        }

        return options
            .OrderBy(ThemeTokenSortGroup)
            .ThenBy(ThemeTokenSortValue)
            .ThenBy((option) => option.Kind, StringComparer.Ordinal)
            .ThenBy((option) => option.Token, StringComparer.Ordinal)
            .ToList();
    }

    public ThemeSettings GetThemeSettings(string themeId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json FROM themes WHERE id = $id";
        command.Parameters.AddWithValue("$id", themeId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException($"Missing theme '{themeId}'.");
        }

        return new ThemeSettings(
            reader.GetString(0),
            reader.GetString(1),
            ReadString(reader, 2),
            ReadString(reader, 3),
            ReadString(reader, 4),
            ReadString(reader, 5),
            ReadString(reader, 6),
            ReadString(reader, 7));
    }

    public string GetThemeFieldValue(string themeId, string fieldId)
    {
        var settings = GetThemeSettings(themeId);
        var tokens = ParseJsonObject(string.IsNullOrWhiteSpace(settings.TokensJson) ? "{}" : settings.TokensJson);
        if (ThemeColorPairPaths.TryGetValue(fieldId, out var colorPairPaths))
        {
            var colors = $"{JsonString(tokens, colorPairPaths.Light)}|{JsonString(tokens, colorPairPaths.Dark)}";
            if (!ThemeAlphaPairPaths.TryGetValue(fieldId, out var alphaPairPaths))
            {
                return colors;
            }

            var lightAlpha = JsonNumberString(tokens, alphaPairPaths.Light, "1");
            var darkAlpha = JsonNumberString(tokens, alphaPairPaths.Dark, "1");
            return $"{colors}||{lightAlpha}|{darkAlpha}";
        }

        if (ThemeNumericTokenCatalog.TryGet(fieldId, out var numericToken))
        {
            return JsonNumberString(tokens, numericToken.Path.ToArray());
        }
        if (ThemeMotionTimingPaths.TryGetValue(fieldId, out var timingPath))
        {
            return GetJsonValue(tokens, timingPath) is JsonObject timing
                ? timing.ToJsonString()
                : "{}";
        }
        if (ThemeMotionEasingPaths.TryGetValue(fieldId, out var easingPath))
        {
            return JsonString(tokens, easingPath);
        }

        return fieldId switch
        {
            "theme.family" => settings.Family,
            "theme.iconThemeId" => settings.IconThemeId,
            "theme.statusBarId" => settings.StatusBarId,
            "theme.navigationBarId" => settings.NavigationBarId,
            "theme.defaultMode" => JsonString(tokens, ["defaultMode"]) is { Length: > 0 } mode ? mode : "light",
            "theme.neutralTint.hueDeg" => JsonNumberString(tokens, ["neutralTint", "hueDeg"]),
            "theme.neutralTint.saturation" => JsonNumberString(tokens, ["neutralTint", "saturation"]),
            "theme.shadows.default.color" => JsonString(tokens, ["shadows", "default", "color", "color"]),
            "theme.typography.fontFamilyId" => JsonString(tokens, ["typography", "fontFamilyId"]),
            "theme.typography.systemFontFamilyId" => JsonString(tokens, ["typography", "systemFontFamilyId"]),
            "theme.typography.emojiFontFamilyId" => JsonString(tokens, ["typography", "emojiFontFamilyId"]),
            "theme.typography.style" => JsonString(tokens, ["typography", "style"]) is { Length: > 0 } style ? style : "normal",
            _ => throw new InvalidOperationException($"Unknown theme field '{fieldId}'."),
        };
    }

    public void UpdateThemeField(string themeId, string fieldId, string value)
    {
        using var connection = OpenConnection();
        switch (fieldId)
        {
            case "theme.family":
                Execute(connection, "UPDATE themes SET family = $value WHERE id = $id", ("$id", themeId), ("$value", value));
                return;
            case "theme.iconThemeId":
                Execute(connection, "UPDATE themes SET icon_theme_id = $value WHERE id = $id", ("$id", themeId), ("$value", value));
                return;
            case "theme.statusBarId":
                Execute(connection, "UPDATE themes SET status_bar_id = $value WHERE id = $id", ("$id", themeId), ("$value", value));
                return;
            case "theme.navigationBarId":
                Execute(connection, "UPDATE themes SET navigation_bar_id = $value WHERE id = $id", ("$id", themeId), ("$value", value));
                return;
            default:
                UpdateThemeToken(connection, themeId, fieldId, value);
                return;
        }
    }

    private static void SeedThemesIfEmpty(SqliteConnection connection)
    {
        var projectIds = QueryProjectRows(connection).Select((project) => project.Id).ToList();
        foreach (var projectId in projectIds)
        {
            if (ScalarLong(connection, "SELECT COUNT(*) FROM themes WHERE project_id = $projectId", ("$projectId", projectId)) > 0)
            {
                continue;
            }

            var iconThemeId = FirstId(connection, "icon_themes", projectId);
            var textFontId = ScalarString(
                connection,
                "SELECT id FROM production_fonts WHERE project_id = $projectId AND category = 'text' ORDER BY family_name, id LIMIT 1",
                ("$projectId", projectId)) ?? "";
            var emojiFontId = ScalarString(
                connection,
                "SELECT id FROM production_fonts WHERE project_id = $projectId AND category = 'emoji' ORDER BY family_name, id LIMIT 1",
                ("$projectId", projectId)) ?? "";
            var statusBarId = DefaultComponentPresetReference(connection, projectId, "status_bar");
            var navigationBarId = DefaultComponentPresetReference(connection, projectId, "navigation_bar");
            Execute(
                connection,
                """
                INSERT INTO themes (id, project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json)
                VALUES ($id, $projectId, $name, $family, $iconThemeId, $statusBarId, $navigationBarId, $tokensJson, $metadataJson)
                """,
                ("$id", $"theme_{projectId}_ios_default"),
                ("$projectId", projectId),
                ("$name", "iOS Default Theme"),
                ("$family", "ios"),
                ("$iconThemeId", iconThemeId),
                ("$statusBarId", statusBarId),
                ("$navigationBarId", navigationBarId),
                ("$tokensJson", DefaultThemeTokensJson("ios", textFontId, emojiFontId)),
                ("$metadataJson", JsonSerializer.Serialize(new { note = "Default iOS-style production theme." })));
        }
    }

    private static void EnsureThemeTokens(SqliteConnection connection)
    {
        foreach (var theme in QueryThemeRows(connection))
        {
            var tokens = ParseJsonObject(string.IsNullOrWhiteSpace(theme.TokensJson) ? "{}" : theme.TokensJson);
            var defaults = ParseJsonObject(DefaultThemeTokensJson(theme.Family));
            var changed = RemoveJsonValue(tokens, ["modes", "light", "keyboard", "popoverBackground"])
                | RemoveJsonValue(tokens, ["modes", "dark", "keyboard", "popoverBackground"])
                | MergeMissing(tokens, defaults);
            if (!changed)
            {
                continue;
            }

            Execute(
                connection,
                "UPDATE themes SET tokens_json = $tokensJson WHERE id = $id",
                ("$id", theme.Id),
                ("$tokensJson", tokens.ToJsonString()));
        }
    }

    private static List<ThemeRow> QueryThemeRows(SqliteConnection connection)
    {
        var rows = new List<ThemeRow>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, name, family, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, metadata_json FROM themes ORDER BY name";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new ThemeRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                ReadString(reader, 3),
                ReadString(reader, 4),
                ReadString(reader, 5),
                ReadString(reader, 6),
                ReadString(reader, 7),
                ReadString(reader, 8)));
        }

        return rows;
    }

    private static string ThemeReferenceSummary(ThemeRow theme)
    {
        return ThemeReferenceSummary(theme.IconThemeId, theme.StatusBarId, theme.NavigationBarId);
    }

    private static string ThemeReferenceSummary(string iconThemeId, string statusBarId, string navigationBarId)
    {
        var linkedCount = new[] { iconThemeId, statusBarId, navigationBarId }.Count((value) => !string.IsNullOrWhiteSpace(value));
        return $"{linkedCount}/3 refs";
    }

    private static IEnumerable<ThemeTokenOption> NumericThemeTokenOptions(JsonObject tokens)
    {
        foreach (var token in ThemeNumericTokenCatalog.NumericTokens)
        {
            yield return new ThemeTokenOption(
                token.Id,
                token.Id.Replace("theme.", "", StringComparison.Ordinal),
                "number",
                JsonNumberString(tokens, token.Path.ToArray(), "—"),
                null,
                null);
        }
    }

    private static int ThemeTokenSortGroup(ThemeTokenOption option)
    {
        return option.Kind.Equals("number", StringComparison.Ordinal)
            ? 1
            : 0;
    }

    private static double ThemeTokenSortValue(ThemeTokenOption option)
    {
        if (!option.Kind.Equals("number", StringComparison.Ordinal))
        {
            return 0;
        }

        var normalized = option.Value.Replace(",", ".", StringComparison.Ordinal);
        return double.TryParse(normalized, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value
            : double.MaxValue;
    }

    private static string? PaletteHex(IReadOnlyDictionary<string, string> palette, string token)
    {
        return palette.TryGetValue(token, out var hex) ? hex : null;
    }

    private static void UpdateThemeToken(SqliteConnection connection, string themeId, string fieldId, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT tokens_json FROM themes WHERE id = $id";
        command.Parameters.AddWithValue("$id", themeId);
        var tokens = ParseJsonObject(command.ExecuteScalar() as string ?? "{}");

        if (ThemeColorPairPaths.TryGetValue(fieldId, out var colorPairPaths))
        {
            if (ThemeAlphaPairPaths.TryGetValue(fieldId, out var alphaPairPaths))
            {
                var pair = PaletteAlphaPair.Split(value);
                SetPair(tokens, $"{pair.First.ColorToken}|{pair.Second.ColorToken}", colorPairPaths.Light, colorPairPaths.Dark, asNumber: false);
                SetPair(tokens, $"{PaletteAlphaPair.FormatAlpha(pair.First.Alpha)}|{PaletteAlphaPair.FormatAlpha(pair.Second.Alpha)}", alphaPairPaths.Light, alphaPairPaths.Dark, asNumber: true);
            }
            else
            {
                SetPair(tokens, value, colorPairPaths.Light, colorPairPaths.Dark, asNumber: false);
            }
            Execute(connection, "UPDATE themes SET tokens_json = $tokensJson WHERE id = $id", ("$id", themeId), ("$tokensJson", tokens.ToJsonString()));
            return;
        }

        if (ThemeNumericTokenCatalog.TryGet(fieldId, out var numericToken))
        {
            JsonPath.Set(tokens, numericToken.Path, NumberNode(value));
            Execute(connection, "UPDATE themes SET tokens_json = $tokensJson WHERE id = $id", ("$id", themeId), ("$tokensJson", tokens.ToJsonString()));
            return;
        }
        if (ThemeMotionTimingPaths.TryGetValue(fieldId, out var timingPath))
        {
            var timing = JsonNode.Parse(value) as JsonObject
                ?? throw new InvalidOperationException($"Theme motion field '{fieldId}' must be a JSON object.");
            SetJsonValue(tokens, timingPath, timing);
            Execute(connection, "UPDATE themes SET tokens_json = $tokensJson WHERE id = $id", ("$id", themeId), ("$tokensJson", tokens.ToJsonString()));
            return;
        }
        if (ThemeMotionEasingPaths.TryGetValue(fieldId, out var easingPath))
        {
            SetJsonValue(tokens, easingPath, JsonValue.Create(value)!);
            Execute(connection, "UPDATE themes SET tokens_json = $tokensJson WHERE id = $id", ("$id", themeId), ("$tokensJson", tokens.ToJsonString()));
            return;
        }

        switch (fieldId)
        {
            case "theme.defaultMode":
                SetJsonValue(tokens, ["defaultMode"], JsonValue.Create(value)!);
                break;
            case "theme.neutralTint.hueDeg":
                SetJsonValue(tokens, ["neutralTint", "hueDeg"], NumberNode(value));
                break;
            case "theme.neutralTint.saturation":
                SetJsonValue(tokens, ["neutralTint", "saturation"], NumberNode(value));
                break;
            case "theme.shadows.default.color":
                SetJsonValue(tokens, ["shadows", "default", "color", "color"], JsonValue.Create(value)!);
                break;
            case "theme.typography.fontFamilyId":
                SetJsonValue(tokens, ["typography", "fontFamilyId"], JsonValue.Create(value)!);
                break;
            case "theme.typography.systemFontFamilyId":
                SetJsonValue(tokens, ["typography", "systemFontFamilyId"], JsonValue.Create(value)!);
                break;
            case "theme.typography.emojiFontFamilyId":
                SetJsonValue(tokens, ["typography", "emojiFontFamilyId"], JsonValue.Create(value)!);
                break;
            case "theme.typography.style":
                SetJsonValue(tokens, ["typography", "style"], JsonValue.Create(value)!);
                break;
            default:
                throw new InvalidOperationException($"Unknown theme field '{fieldId}'.");
        }

        Execute(connection, "UPDATE themes SET tokens_json = $tokensJson WHERE id = $id", ("$id", themeId), ("$tokensJson", tokens.ToJsonString()));
    }

    private static string DefaultThemeTokensJson(
        string family,
        string textFontFamilyId = "",
        string emojiFontFamilyId = "")
    {
        var isAndroid = family.Equals("android", StringComparison.OrdinalIgnoreCase);
        var tokens = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["defaultMode"] = "light",
            ["neutralTint"] = new JsonObject
            {
                ["hueDeg"] = 0,
                ["saturation"] = 0,
            },
            ["cursor"] = new JsonObject
            {
                ["width"] = 2,
                ["blinkDurationMs"] = 800,
            },
            ["keyboard"] = new JsonObject
            {
                ["height"] = isAndroid ? 280 : 260,
                ["keyGap"] = isAndroid ? 3 : 4,
                ["rowGap"] = isAndroid ? 4 : 6,
            },
            ["typography"] = new JsonObject
            {
                ["fontFamilyId"] = textFontFamilyId,
                ["systemFontFamilyId"] = textFontFamilyId,
                ["emojiFontFamilyId"] = emojiFontFamilyId,
                ["size"] = isAndroid ? 15 : 16,
                ["sizes"] = new JsonObject
                {
                    ["xs"] = 10,
                    ["s"] = 12,
                    ["m"] = isAndroid ? 15 : 16,
                    ["l"] = isAndroid ? 18 : 19,
                    ["xl"] = isAndroid ? 22 : 24,
                },
                ["weight"] = 400,
                ["style"] = "normal",
                ["lineHeights"] = new JsonObject
                {
                    ["tight"] = 1,
                    ["compact"] = 1.1,
                    ["normal"] = 1.2,
                    ["relaxed"] = 1.35,
                    ["loose"] = 1.5,
                },
            },
            ["spacing"] = new JsonObject
            {
                ["none"] = 0,
                ["xs"] = 2,
                ["s"] = 4,
                ["m"] = 8,
                ["l"] = 12,
                ["xl"] = 16,
                ["xxl"] = 24,
            },
            ["iconSizes"] = new JsonObject
            {
                ["xs"] = 12,
                ["s"] = 16,
                ["m"] = 20,
                ["l"] = 24,
                ["xl"] = 32,
            },
            ["radii"] = new JsonObject
            {
                ["none"] = 0,
                ["xs"] = 2,
                ["s"] = 4,
                ["m"] = 8,
                ["l"] = 12,
                ["xl"] = 16,
                ["xxl"] = 24,
                ["full"] = 999,
            },
            ["shadows"] = new JsonObject
            {
                ["default"] = new JsonObject
                {
                    ["color"] = new JsonObject
                    {
                        ["color"] = "gray_000",
                        ["alpha"] = 0.18,
                    },
                    ["offsetX"] = 0,
                    ["offsetY"] = 4,
                    ["blur"] = 18,
                },
            },
            ["motion"] = ThemeMotionTokens(),
            ["modes"] = new JsonObject
            {
                ["light"] = new JsonObject
                {
                    ["colors"] = new JsonObject
                    {
                        ["background"] = "gray_100",
                        ["surface"] = "gray_100",
                        ["card"] = "gray_100",
                        ["label"] = "gray_040",
                        ["text"] = "gray_010",
                        ["textPrimary"] = "gray_010",
                        ["textSecondary"] = "gray_040",
                        ["icon"] = "gray_010",
                        ["button"] = isAndroid ? "purple" : "blue",
                        ["field"] = "gray_100",
                        ["checkbox"] = isAndroid ? "purple" : "blue",
                        ["radio"] = isAndroid ? "purple" : "blue",
                        ["switch"] = isAndroid ? "purple" : "blue",
                        ["tab"] = isAndroid ? "purple" : "blue",
                        ["menuItem"] = "gray_010",
                        ["badge"] = "red",
                        ["toast"] = "gray_020",
                        ["divider"] = "gray_080",
                        ["accent"] = isAndroid ? "purple" : "blue",
                        ["icons.primary"] = "gray_010",
                        ["icons.secondary"] = "gray_040",
                        ["icons.alternate"] = "gray_060",
                        ["icons.accent"] = isAndroid ? "purple" : "blue",
                        ["borders.primary"] = "gray_070",
                        ["borders.secondary"] = "gray_080",
                        ["borders.alternate"] = "gray_060",
                        ["theme.cursor.color"] = isAndroid ? "purple" : "blue",
                    },
                    ["keyboard"] = new JsonObject
                    {
                        ["background"] = "gray_090",
                        ["keyBackground"] = "gray_100",
                        ["specialKeyBackground"] = "gray_080",
                        ["pressedKeyBackground"] = "gray_070",
                        ["keyBorder"] = "gray_080",
                        ["text"] = "gray_010",
                    },
                },
                ["dark"] = new JsonObject
                {
                    ["colors"] = new JsonObject
                    {
                        ["background"] = "gray_010",
                        ["surface"] = "gray_020",
                        ["card"] = "gray_030",
                        ["label"] = "gray_070",
                        ["text"] = "gray_100",
                        ["textPrimary"] = "gray_100",
                        ["textSecondary"] = "gray_070",
                        ["icon"] = "gray_100",
                        ["button"] = isAndroid ? "purple_tint" : "blue_bright",
                        ["field"] = "gray_030",
                        ["checkbox"] = isAndroid ? "purple_tint" : "blue_bright",
                        ["radio"] = isAndroid ? "purple_tint" : "blue_bright",
                        ["switch"] = isAndroid ? "purple_tint" : "blue_bright",
                        ["tab"] = isAndroid ? "purple_tint" : "blue_bright",
                        ["menuItem"] = "gray_100",
                        ["badge"] = "red",
                        ["toast"] = "gray_030",
                        ["divider"] = "gray_040",
                        ["accent"] = isAndroid ? "purple_tint" : "blue_bright",
                        ["icons.primary"] = "gray_100",
                        ["icons.secondary"] = "gray_070",
                        ["icons.alternate"] = "gray_050",
                        ["icons.accent"] = isAndroid ? "purple_tint" : "blue_bright",
                        ["borders.primary"] = "gray_040",
                        ["borders.secondary"] = "gray_030",
                        ["borders.alternate"] = "gray_050",
                        ["theme.cursor.color"] = isAndroid ? "purple_tint" : "blue_bright",
                    },
                    ["keyboard"] = new JsonObject
                    {
                        ["background"] = "gray_020",
                        ["keyBackground"] = "gray_030",
                        ["specialKeyBackground"] = "gray_040",
                        ["pressedKeyBackground"] = "gray_050",
                        ["keyBorder"] = "gray_040",
                        ["text"] = "gray_100",
                    },
                },
            },
        };
        return tokens.ToJsonString();
    }

    private static JsonObject ThemeMotionTokens()
    {
        return new JsonObject
        {
            ["transitions"] = new JsonObject
            {
                ["fade"] = MotionTiming(180, 0, "ease", 1),
                ["slide"] = MotionTiming(260, 0, "ease-out", 1),
                ["swipe"] = MotionTiming(220, 0, "ease-out", 1),
                ["scale"] = MotionTiming(220, 0, "ease", 1),
            },
        };
    }

    private static JsonObject MotionTiming(int durationMs, int delayMs, string easing, double intensity)
    {
        return new JsonObject
        {
            ["durationMs"] = durationMs,
            ["delayMs"] = delayMs,
            ["easing"] = easing,
            ["intensity"] = intensity,
        };
    }
}
