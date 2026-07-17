using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public ActorSettings GetActorSettings(string actorId)
    {
        return _actorRepository.GetSettings(actorId);
    }

    public void UpdateActorField(string actorId, string fieldId, string value)
    {
        _actorRepository.UpdateField(actorId, fieldId, value);
    }

    public string GetActorFieldValue(string actorId, string fieldId)
    {
        var settings = GetActorSettings(actorId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        return fieldId switch
        {
            "actor.shortName" => settings.ShortName,
            "actor.defaultDeviceId" => settings.DefaultDeviceId,
            "actor.defaultThemeId" => settings.DefaultThemeId,
            "actor.color.modes" => MetricPair(settings.MetadataJson, ["modes", "light", "color"], ["modes", "dark", "color"]),
            "actor.avatarTextColor.modes" => MetricPair(settings.MetadataJson, ["modes", "light", "avatarTextColor"], ["modes", "dark", "avatarTextColor"]),
            "actor.wallpaper.images.light.filePath" => JsonString(metadata, ["wallpaper", "images", "light", "filePath"]),
            "actor.wallpaper.images.dark.filePath" => JsonString(metadata, ["wallpaper", "images", "dark", "filePath"]),
            "actor.wallpaper.kind" => JsonString(metadata, ["wallpaper", "kind"]),
            "actor.wallpaper.opacity" => JsonNumberString(metadata, ["wallpaper", "opacity"]),
            "actor.wallpaper.color" => MetricPair(settings.MetadataJson, ["modes", "light", "wallpaper", "color"], ["modes", "dark", "wallpaper", "color"]),
            "actor.avatar.filePath" => JsonString(metadata, ["avatar", "filePath"]),
            "actor.avatar.scale" => JsonNumberString(metadata, ["avatar", "scale"]),
            "actor.avatar.offset" => MetricPair(settings.MetadataJson, ["avatar", "offsetX"], ["avatar", "offsetY"]),
            "actor.avatar.useInitials" => BoolToString(JsonBool(metadata, ["avatar", "useInitials"])),
            "actor.avatar.initialsPadding" => JsonNumberString(metadata, ["avatar", "initialsPadding"]),
            _ => throw new InvalidOperationException($"Unknown actor field '{fieldId}'."),
        };
    }

    public IReadOnlyList<FieldOption> GetActorOptions(string projectId)
    {
        var options = _actorRepository.GetOptions(projectId)
            .Select((option) => new FieldOption(option.Value, option.Label))
            .ToList();
        options.Insert(0, new FieldOption("", "None"));
        return options;
    }

    private IReadOnlyList<ActorRecord> QueryActorRows(SqliteConnection connection)
    {
        return _actorRepository.QueryAll(connection);
    }
}
