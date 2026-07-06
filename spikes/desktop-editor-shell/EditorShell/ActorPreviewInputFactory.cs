using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ActorPreviewInputFactory
{
    public static JsonObject Create(
        SpikeDatabase database,
        string actorId,
        string themeMode,
        IReadOnlyDictionary<string, string> paletteColors)
    {
        var settings = database.GetActorSettings(actorId);
        var mediaRoot = database.GetProjectSettings(settings.ProjectId).MediaRoot;
        var colorToken = ModeValue(database.GetActorFieldValue(actorId, "actor.color.modes"), themeMode);
        var textColorToken = ModeValue(database.GetActorFieldValue(actorId, "actor.avatarTextColor.modes"), themeMode);
        var filePath = database.GetActorFieldValue(actorId, "actor.avatar.filePath");
        var useInitials = BooleanText.Parse(database.GetActorFieldValue(actorId, "actor.avatar.useInitials"));
        var offset = SplitPair(database.GetActorFieldValue(actorId, "actor.avatar.offset"));
        var fullPath = ProjectPathService.ResolveLocalPath(filePath, mediaRoot);
        var imageUri = !useInitials && !string.IsNullOrWhiteSpace(fullPath) && File.Exists(fullPath)
            ? DataUri(fullPath)
            : "";

        return new JsonObject
        {
            ["id"] = actorId,
            ["displayName"] = settings.DisplayName,
            ["shortName"] = settings.ShortName,
            ["initials"] = Initials(settings.ShortName, settings.DisplayName),
            ["avatar"] = new JsonObject
            {
                ["imageUri"] = imageUri,
                ["backgroundColor"] = PaletteColor(paletteColors, colorToken, actorId, "actor.color.modes"),
                ["textColor"] = PaletteColor(paletteColors, textColorToken, actorId, "actor.avatarTextColor.modes"),
                ["scale"] = NumericText.Double(database.GetActorFieldValue(actorId, "actor.avatar.scale"), 1),
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

    private static string Initials(string shortName, string displayName)
    {
        var source = string.IsNullOrWhiteSpace(shortName) ? displayName : shortName;
        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(parts.Take(2).Select((part) => part[0])).ToUpperInvariant();
    }
}
