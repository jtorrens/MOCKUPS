using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ActorPreviewInputFactory
{
    public static JsonObject CreateSample()
    {
        return new JsonObject
        {
            ["id"] = "sample_actor",
            ["displayName"] = "Sample",
            ["shortName"] = "Sample",
            ["initials"] = "S",
            ["avatar"] = new JsonObject
            {
                ["imageUri"] = "",
                ["backgroundColor"] = "#B8C8DE",
                ["textColor"] = "#1A1A1A",
                ["scale"] = 1,
                ["offsetX"] = 0,
                ["offsetY"] = 0,
                ["baseSize"] = 640,
            },
        };
    }

    public static JsonObject Create(
        ActorPreviewDataSource dataSource,
        string actorId,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors)
    {
        var source = dataSource.LoadPreview(actorId);
        var metadata = JsonPath.ParseRequiredObject(source.MetadataJson, $"Actor '{actorId}' metadata");
        var colorToken = ModeValue(source.ColorModes, themeMode);
        var textColorToken = ModeValue(source.AvatarTextColorModes, themeMode);
        var useInitials = BooleanText.Parse(source.AvatarUseInitials);
        var offset = SplitPair(source.AvatarOffset);
        var fullPath = ProjectPathService.ResolveLocalPath(source.AvatarFilePath, source.ProjectMediaRoot);
        var imageUri = !useInitials && !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath)
            ? DataUri(fullPath)
            : "";

        return new JsonObject
        {
            ["id"] = actorId,
            ["displayName"] = source.DisplayName,
            ["shortName"] = source.ShortName,
            ["initials"] = ActorIdentityText.Initials(source.ShortName, source.DisplayName),
            ["wallpaper"] = metadata["wallpaper"]?.DeepClone(),
            ["modes"] = metadata["modes"]?.DeepClone(),
            ["avatar"] = new JsonObject
            {
                ["imageUri"] = imageUri,
                ["backgroundColor"] = PaletteColor(paletteColors, colorToken, actorId, "actor.color.modes"),
                ["textColor"] = PaletteColor(paletteColors, textColorToken, actorId, "actor.avatarTextColor.modes"),
                ["scale"] = NumericText.Double(source.AvatarScale, 1),
                ["offsetX"] = NumericText.Double(offset.First, 0),
                ["offsetY"] = NumericText.Double(offset.Second, 0),
                ["baseSize"] = 640,
            },
        };
    }

    private static string ModeValue(string pair, string themeMode)
    {
        var values = SplitPair(pair);
        return string.Equals(themeMode, "dark", StringComparison.OrdinalIgnoreCase)
            ? values.Second
            : values.First;
    }

    private static string PaletteColor(
        IReadOnlyDictionary<string, string> paletteColors,
        string token,
        string actorId,
        string fieldId)
    {
        if (paletteColors.TryGetValue(token, out var color) && !string.IsNullOrWhiteSpace(color))
        {
            return color;
        }

        throw new InvalidOperationException($"Missing palette color '{token}' for {fieldId} in actor '{actorId}'.");
    }

    private static string DataUri(string fullPath)
    {
        var mimeType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream",
        };
        return $"data:{mimeType};base64,{Convert.ToBase64String(File.ReadAllBytes(fullPath))}";
    }

    private static (string First, string Second) SplitPair(string value)
    {
        var parts = value.Split('|', 2);
        return (parts.ElementAtOrDefault(0) ?? "", parts.ElementAtOrDefault(1) ?? "");
    }

}
