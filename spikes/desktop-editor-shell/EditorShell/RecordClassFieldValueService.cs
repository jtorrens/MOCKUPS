using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RecordClassFieldValueService
{
    private readonly SpikeDatabase _database;

    public RecordClassFieldValueService(SpikeDatabase database)
    {
        _database = database;
    }

    public bool CanHandle(ProjectTreeNodeKind nodeKind, string fieldId)
    {
        return nodeKind switch
        {
            ProjectTreeNodeKind.Project => fieldId.StartsWith("project.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Episode => fieldId.StartsWith("episode.", StringComparison.Ordinal),
            ProjectTreeNodeKind.PaletteColor => fieldId.StartsWith("palette.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Device => fieldId.StartsWith("device.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Theme => fieldId.StartsWith("theme.", StringComparison.Ordinal),
            ProjectTreeNodeKind.Actor => fieldId.StartsWith("actor.", StringComparison.Ordinal),
            ProjectTreeNodeKind.ProductionFont => fieldId.StartsWith("font.", StringComparison.Ordinal),
            ProjectTreeNodeKind.IconTheme => fieldId.StartsWith("iconTheme.", StringComparison.Ordinal),
            ProjectTreeNodeKind.StatusBar => fieldId.StartsWith("statusBar.", StringComparison.Ordinal),
            ProjectTreeNodeKind.NavigationBar => fieldId.StartsWith("navigationBar.", StringComparison.Ordinal),
            _ => false,
        };
    }

    public FieldValue CreateFieldValue(ProjectTreeNode node, string fieldId)
    {
        var field = RecordClassFieldCatalog.Get(fieldId);
        var value = node.Kind switch
        {
            ProjectTreeNodeKind.Project => ProjectFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Episode => EpisodeFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.PaletteColor => PaletteColorFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Device => DeviceFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Theme => ThemeFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.Actor => ActorFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.ProductionFont => ProductionFontFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.IconTheme => IconThemeFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.StatusBar => StatusBarFieldValue(node.Id, field.Id),
            ProjectTreeNodeKind.NavigationBar => NavigationBarFieldValue(node.Id, field.Id),
            _ => throw new InvalidOperationException($"Record class field '{fieldId}' is not supported for '{node.Kind}'."),
        };
        var options = node.Kind switch
        {
            ProjectTreeNodeKind.Theme => ThemeFieldOptions(node.Id, field),
            ProjectTreeNodeKind.Actor => ActorFieldOptions(node.Id, field),
            ProjectTreeNodeKind.ProductionFont => ProductionFontFieldOptions(field),
            ProjectTreeNodeKind.NavigationBar => NavigationBarFieldOptions(field),
            _ => field.Options,
        };

        return new FieldValue(
            new FieldDefinition(
                field.Id,
                field.Label,
                field.ValueKind,
                IsEditable: field.IsEditable,
                DefaultValue: DefaultValue(node.Kind, field, value),
                CommitAsDefault: CommitAsDefault(node.Kind, field),
                Options: options),
            value);
    }

    public void CommitFieldValue(ProjectTreeNode node, string fieldId, string value)
    {
        switch (node.Kind)
        {
            case ProjectTreeNodeKind.Project when fieldId.StartsWith("project.", StringComparison.Ordinal):
                _database.UpdateProjectField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Episode when fieldId.StartsWith("episode.", StringComparison.Ordinal):
                _database.UpdateEpisodeField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.PaletteColor when fieldId.StartsWith("palette.", StringComparison.Ordinal):
                _database.UpdatePaletteColorField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Device when fieldId.StartsWith("device.", StringComparison.Ordinal):
                _database.UpdateDeviceField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Theme when fieldId.StartsWith("theme.", StringComparison.Ordinal):
                _database.UpdateThemeField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.Actor when fieldId.StartsWith("actor.", StringComparison.Ordinal):
                _database.UpdateActorField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.ProductionFont when fieldId.StartsWith("font.", StringComparison.Ordinal):
                _database.UpdateProductionFontField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.IconTheme when fieldId.StartsWith("iconTheme.", StringComparison.Ordinal):
                return;
            case ProjectTreeNodeKind.StatusBar when fieldId.StartsWith("statusBar.", StringComparison.Ordinal):
                _database.UpdateStatusBarField(node.Id, fieldId, value);
                return;
            case ProjectTreeNodeKind.NavigationBar when fieldId.StartsWith("navigationBar.", StringComparison.Ordinal):
                _database.UpdateNavigationBarField(node.Id, fieldId, value);
                return;
            default:
                throw new InvalidOperationException($"Record class field '{fieldId}' is not supported for '{node.Kind}'.");
        }
    }

    private string ProjectFieldValue(string projectId, string fieldId)
    {
        var settings = _database.GetProjectSettings(projectId);
        return fieldId switch
        {
            "project.slug" => settings.Slug,
            "project.defaultFps" => settings.DefaultFps.ToString(),
            "project.mediaRoot" => settings.MediaRoot,
            _ => throw new InvalidOperationException($"Unknown project field '{fieldId}'."),
        };
    }

    private string EpisodeFieldValue(string episodeId, string fieldId)
    {
        var settings = _database.GetEpisodeSettings(episodeId);
        return fieldId switch
        {
            "episode.slug" => settings.Slug,
            "episode.sortOrder" => settings.SortOrder.ToString(),
            _ => throw new InvalidOperationException($"Unknown episode field '{fieldId}'."),
        };
    }

    private string PaletteColorFieldValue(string colorId, string fieldId)
    {
        var settings = _database.GetPaletteColorSettings(colorId);
        return fieldId switch
        {
            "palette.token" => settings.Token,
            "palette.valueHex" => settings.ValueHex,
            "palette.isNeutral" => BoolToString(settings.IsNeutral),
            "palette.source" => settings.Source,
            "palette.protected" => BoolToString(settings.IsProtected),
            "palette.hiddenFromPickers" => BoolToString(settings.HiddenFromPickers),
            "palette.note" => settings.Note,
            _ => throw new InvalidOperationException($"Unknown palette field '{fieldId}'."),
        };
    }

    private string DeviceFieldValue(string deviceId, string fieldId)
    {
        var settings = _database.GetDeviceSettings(deviceId);
        return fieldId switch
        {
            "device.manufacturer" => settings.Manufacturer,
            "device.model" => settings.Model,
            "device.osFamily" => settings.OsFamily,
            _ => _database.GetDeviceMetricFieldValue(deviceId, fieldId),
        };
    }

    private string ActorFieldValue(string actorId, string fieldId)
    {
        return _database.GetActorFieldValue(actorId, fieldId);
    }

    private string ThemeFieldValue(string themeId, string fieldId)
    {
        return _database.GetThemeFieldValue(themeId, fieldId);
    }

    private string ProductionFontFieldValue(string fontId, string fieldId)
    {
        return _database.GetProductionFontFieldValue(fontId, fieldId);
    }

    private string IconThemeFieldValue(string iconThemeId, string fieldId)
    {
        return _database.GetIconThemeFieldValue(iconThemeId, fieldId);
    }

    private string StatusBarFieldValue(string statusBarId, string fieldId)
    {
        return _database.GetStatusBarFieldValue(statusBarId, fieldId);
    }

    private string NavigationBarFieldValue(string navigationBarId, string fieldId)
    {
        return _database.GetNavigationBarFieldValue(navigationBarId, fieldId);
    }

    private static IReadOnlyList<FieldOption>? ProductionFontFieldOptions(RecordClassFieldDescriptor field)
    {
        return field.Id == "font.category"
            ?
            [
                new FieldOption("text", "Text"),
                new FieldOption("emoji", "Emoji"),
            ]
            : field.Options;
    }

    private static IReadOnlyList<FieldOption>? NavigationBarFieldOptions(RecordClassFieldDescriptor field)
    {
        return field.Id == "navigationBar.type"
            ?
            [
                new FieldOption("buttons", "Buttons"),
                new FieldOption("gestureBar", "Gesture Bar"),
            ]
            : field.Options;
    }

    private IReadOnlyList<FieldOption>? ThemeFieldOptions(string themeId, RecordClassFieldDescriptor field)
    {
        var settings = _database.GetThemeSettings(themeId);
        return field.Id switch
        {
            "theme.family" =>
            [
                new FieldOption("ios", "iOS"),
                new FieldOption("android", "Android"),
                new FieldOption("custom", "Custom"),
            ],
            "theme.iconThemeId" => _database.GetIconThemeOptions(settings.ProjectId),
            "theme.statusBarId" => _database.GetStatusBarOptions(settings.ProjectId),
            "theme.navigationBarId" => _database.GetNavigationBarOptions(settings.ProjectId),
            "theme.defaultMode" =>
            [
                new FieldOption("light", "Light"),
                new FieldOption("dark", "Dark"),
            ],
            "theme.typography.fontFamilyId" => _database.GetProductionFontOptions(settings.ProjectId, "text"),
            "theme.typography.emojiFontFamilyId" => _database.GetProductionFontOptions(settings.ProjectId, "emoji"),
            "theme.typography.weight" =>
            [
                new FieldOption("100", "100"),
                new FieldOption("200", "200"),
                new FieldOption("300", "300"),
                new FieldOption("400", "400"),
                new FieldOption("500", "500"),
                new FieldOption("600", "600"),
                new FieldOption("700", "700"),
                new FieldOption("800", "800"),
                new FieldOption("900", "900"),
            ],
            "theme.typography.style" =>
            [
                new FieldOption("normal", "Normal"),
                new FieldOption("italic", "Italic"),
            ],
            _ => field.ValueKind is ValueKind.PaletteColorToken or ValueKind.PaletteColorPair
                ? _database.GetPaletteColorOptions(settings.ProjectId)
                : field.Options,
        };
    }

    private IReadOnlyList<FieldOption>? ActorFieldOptions(string actorId, RecordClassFieldDescriptor field)
    {
        var settings = _database.GetActorSettings(actorId);
        return field.Id switch
        {
            "actor.defaultDeviceId" => _database.GetDeviceOptions(settings.ProjectId),
            "actor.defaultThemeId" => _database.GetThemeOptions(settings.ProjectId),
            _ => field.ValueKind is ValueKind.PaletteColorToken or ValueKind.PaletteColorPair
                ? _database.GetPaletteColorOptions(settings.ProjectId)
                : field.Options,
        };
    }

    private static string DefaultValue(ProjectTreeNodeKind nodeKind, RecordClassFieldDescriptor field, string currentValue)
    {
        if (nodeKind != ProjectTreeNodeKind.Actor)
        {
            return currentValue;
        }

        return field.Id switch
        {
            "actor.avatar.filePath" => "",
            "actor.avatar.scale" => "1",
            "actor.avatar.offset" => "0|0",
            "actor.avatar.useInitials" => "false",
            "actor.avatar.initialsPadding" => "96",
            _ => currentValue,
        };
    }

    private static bool CommitAsDefault(ProjectTreeNodeKind nodeKind, RecordClassFieldDescriptor field)
    {
        if (nodeKind != ProjectTreeNodeKind.Actor)
        {
            return true;
        }

        return !field.Id.StartsWith("actor.avatar.", StringComparison.Ordinal)
            && field.ValueKind != ValueKind.PaletteColorPair;
    }

    private static string BoolToString(bool value) => value ? "true" : "false";
}
