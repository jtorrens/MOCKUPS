using Mockups.DesktopEditorShell.Data;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record ActorPreviewContextSource(
    string ProjectId,
    string DisplayName,
    string DefaultDeviceId,
    string DefaultThemeId);

internal sealed record ActorPreviewSource(
    string ProjectId,
    string DisplayName,
    string ShortName,
    string MetadataJson,
    string ProjectMediaRoot,
    string ColorModes,
    string AvatarTextColorModes,
    string AvatarFilePath,
    string AvatarScale,
    string AvatarOffset,
    string AvatarUseInitials,
    string AvatarInitialsPadding);

internal sealed class ActorPreviewDataSource
{
    private readonly SpikeDatabase _database;

    public ActorPreviewDataSource(SpikeDatabase database)
    {
        _database = database;
    }

    public ActorPreviewContextSource LoadContext(string actorId)
    {
        var settings = _database.GetActorSettings(actorId);
        return new ActorPreviewContextSource(
            settings.ProjectId,
            settings.DisplayName,
            settings.DefaultDeviceId,
            settings.DefaultThemeId);
    }

    public ActorPreviewSource LoadPreview(string actorId)
    {
        var settings = _database.GetActorSettings(actorId);
        return new ActorPreviewSource(
            settings.ProjectId,
            settings.DisplayName,
            settings.ShortName,
            settings.MetadataJson,
            _database.GetProjectSettings(settings.ProjectId).MediaRoot,
            _database.GetActorFieldValue(actorId, "actor.color.modes"),
            _database.GetActorFieldValue(actorId, "actor.avatarTextColor.modes"),
            _database.GetActorFieldValue(actorId, "actor.avatar.filePath"),
            _database.GetActorFieldValue(actorId, "actor.avatar.scale"),
            _database.GetActorFieldValue(actorId, "actor.avatar.offset"),
            _database.GetActorFieldValue(actorId, "actor.avatar.useInitials"),
            _database.GetActorFieldValue(actorId, "actor.avatar.initialsPadding"));
    }

    public IReadOnlyList<FieldOption> Options(string projectId, bool includeNone = true)
    {
        return includeNone
            ? _database.GetActorOptions(projectId)
            : _database.GetRequiredActorOptions(projectId);
    }

    public IReadOnlyList<FieldOption> PaletteColorOptions(string projectId)
    {
        return _database.GetPaletteColorOptions(projectId);
    }
}
