using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

public enum EditorAddOperationKind
{
    CreateRecord,
    ImportDevice,
    ImportProductionFont,
    RefreshIconThemes,
    SelectModuleInstance,
}
public sealed record EditorAddOperationDefinition(
    string Id,
    EditorAddOperationKind Kind,
    string Label,
    string CreationId = "");

public static class EditorAddOperationCatalog
{
    private static readonly IReadOnlyDictionary<ProjectTreeNodeKind, EditorAddOperationDefinition>
        Definitions = new Dictionary<ProjectTreeNodeKind, EditorAddOperationDefinition>
        {
            [ProjectTreeNodeKind.PaletteRoot] = new("palette.create", EditorAddOperationKind.CreateRecord, "Add palette color", "palette"),
            [ProjectTreeNodeKind.IconThemesRoot] = new("icon-themes.refresh", EditorAddOperationKind.RefreshIconThemes, "Refresh icon sets"),
            [ProjectTreeNodeKind.DevicesRoot] = new("device.import", EditorAddOperationKind.ImportDevice, "Import device"),
            [ProjectTreeNodeKind.ActorsRoot] = new("actor.create", EditorAddOperationKind.CreateRecord, "Add actor", "actor"),
            [ProjectTreeNodeKind.ThemesRoot] = new("theme.create", EditorAddOperationKind.CreateRecord, "Add theme", "theme"),
            [ProjectTreeNodeKind.ProductionFontsRoot] = new("production-font.import", EditorAddOperationKind.ImportProductionFont, "Import production font"),
            [ProjectTreeNodeKind.EpisodesRoot] = new("episode.create", EditorAddOperationKind.CreateRecord, "Add episode", "episode"),
            [ProjectTreeNodeKind.Episode] = new("shot.create", EditorAddOperationKind.CreateRecord, "Add shot", "shot"),
            [ProjectTreeNodeKind.Shot] = new("module-instance.select", EditorAddOperationKind.SelectModuleInstance, "Add screen"),
        };

    public static bool TryGet(
        ProjectTreeNodeKind parentKind,
        out EditorAddOperationDefinition definition) =>
        Definitions.TryGetValue(parentKind, out definition!);

    public static EditorAddOperationDefinition Require(ProjectTreeNodeKind parentKind) =>
        TryGet(parentKind, out var definition)
            ? definition
            : throw new InvalidOperationException(
                $"{parentKind} has no declared Add operation.");
}
