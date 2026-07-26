using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
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
        if (fieldId == "actor.defaultThemeId")
        {
            using var connection = OpenConnection();
            _moduleInstanceThemeContextService.RequireActorThemeChange(connection, actorId, value);
        }
        _actorRepository.UpdateField(actorId, fieldId, value);
    }

    public string GetActorFieldValue(string actorId, string fieldId)
    {
        var settings = GetActorSettings(actorId);
        var metadata = ParseJsonObject(settings.MetadataJson);
        var context = $"Actor '{actorId}' metadata_json";
        return fieldId switch
        {
            "actor.shortName" => settings.ShortName,
            "actor.defaultDeviceId" => settings.DefaultDeviceId,
            "actor.defaultThemeId" => settings.DefaultThemeId,
            "actor.color.modes" => RequiredStringPair(settings.MetadataJson, ["modes", "light", "color"], ["modes", "dark", "color"], context),
            "actor.avatarTextColor.modes" => RequiredStringPair(settings.MetadataJson, ["modes", "light", "avatarTextColor"], ["modes", "dark", "avatarTextColor"], context),
            "actor.wallpaper.images.light.filePath" => JsonString(metadata, ["wallpaper", "images", "light", "filePath"]),
            "actor.wallpaper.images.dark.filePath" => JsonString(metadata, ["wallpaper", "images", "dark", "filePath"]),
            "actor.wallpaper.kind" => JsonString(metadata, ["wallpaper", "kind"]),
            "actor.wallpaper.opacity" => JsonPath.RequiredNumberString(metadata, ["wallpaper", "opacity"], context),
            "actor.wallpaper.color" => RequiredStringPair(settings.MetadataJson, ["modes", "light", "wallpaper", "color"], ["modes", "dark", "wallpaper", "color"], context),
            "actor.avatar.filePath" => JsonString(metadata, ["avatar", "filePath"]),
            "actor.avatar.scale" => JsonPath.RequiredNumberString(metadata, ["avatar", "scale"], context),
            "actor.avatar.offset" => RequiredNumberPair(settings.MetadataJson, ["avatar", "offsetX"], ["avatar", "offsetY"], context),
            "actor.avatar.useInitials" => JsonPath.RequiredBooleanString(metadata, ["avatar", "useInitials"], context),
            "actor.avatar.initialsPadding" => JsonPath.RequiredNumberString(metadata, ["avatar", "initialsPadding"], context),
            _ => throw new InvalidOperationException($"Unknown actor field '{fieldId}'."),
        };
    }

    public IReadOnlyList<FieldOption> GetActorOptions(string projectId)
    {
        var options = GetRequiredActorOptions(projectId).ToList();
        options.Insert(0, new FieldOption("", "None"));
        return options;
    }

    public IReadOnlyList<FieldOption> GetRequiredActorOptions(string projectId)
    {
        return _actorRepository.GetOptions(projectId)
            .Select((option) => new FieldOption(option.Value, option.Label))
            .ToList();
    }

    private IReadOnlyList<ActorRecord> QueryActorRows(SqliteConnection connection)
    {
        return _actorRepository.QueryAll(connection);
    }
}
