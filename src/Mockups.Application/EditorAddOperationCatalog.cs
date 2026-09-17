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
    private static readonly EditorAddOperationDefinition ModuleCreation =
        new("module.create", EditorAddOperationKind.CreateRecord, "Add module", "module");

    private static readonly IReadOnlyDictionary<ProjectTreeNodeKind, EditorAddOperationDefinition>
        Definitions = new Dictionary<ProjectTreeNodeKind, EditorAddOperationDefinition>
        {
            [ProjectTreeNodeKind.ProjectsRoot] = new("project.create", EditorAddOperationKind.CreateRecord, "New project", "project"),
            [ProjectTreeNodeKind.PaletteRoot] = new("palette.create", EditorAddOperationKind.CreateRecord, "Add System palette color", "palette"),
            [ProjectTreeNodeKind.IconThemesRoot] = new("icon-themes.refresh", EditorAddOperationKind.RefreshIconThemes, "Refresh icon sets"),
            [ProjectTreeNodeKind.DevicesRoot] = new("device.import", EditorAddOperationKind.ImportDevice, "Import device"),
            [ProjectTreeNodeKind.ActorsRoot] = new("actor.create", EditorAddOperationKind.CreateRecord, "Add actor", "actor"),
            [ProjectTreeNodeKind.ThemesRoot] = new("theme.create", EditorAddOperationKind.CreateRecord, "Add theme", "theme"),
            [ProjectTreeNodeKind.ProductionFontsRoot] = new("production-font.import", EditorAddOperationKind.ImportProductionFont, "Import production font"),
            [ProjectTreeNodeKind.EpisodesRoot] = new("episode.create", EditorAddOperationKind.CreateRecord, "Add episode", "episode"),
            [ProjectTreeNodeKind.Episode] = new("shot.create", EditorAddOperationKind.CreateRecord, "Add shot", "shot"),
            [ProjectTreeNodeKind.Shot] = new("module-instance.select", EditorAddOperationKind.SelectModuleInstance, "Add screen"),
            [ProjectTreeNodeKind.ComponentClass] = new("variant.create", EditorAddOperationKind.CreateRecord, "Add variant", "variant"),
            [ProjectTreeNodeKind.Module] = new("variant.create", EditorAddOperationKind.CreateRecord, "Add variant", "variant"),
        };

    private static readonly IReadOnlyDictionary<string, EditorAddOperationDefinition>
        DeclaredDefinitions = new Dictionary<string, EditorAddOperationDefinition>(StringComparer.Ordinal)
        {
            [ModuleCreation.Id] = ModuleCreation,
        };

    public static bool TryGet(
        ProjectTreeNode parent,
        out EditorAddOperationDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(parent.DeclaredAddOperationId))
        {
            return DeclaredDefinitions.TryGetValue(
                parent.DeclaredAddOperationId,
                out definition!);
        }

        return TryGet(parent.Kind, out definition);
    }

    public static bool TryGet(
        ProjectTreeNodeKind parentKind,
        out EditorAddOperationDefinition definition) =>
        Definitions.TryGetValue(parentKind, out definition!);

    public static EditorAddOperationDefinition Require(ProjectTreeNodeKind parentKind) =>
        TryGet(parentKind, out var definition)
            ? definition
            : throw new InvalidOperationException(
                $"{parentKind} has no declared Add operation.");

    public static EditorAddOperationDefinition Require(ProjectTreeNode parent) =>
        TryGet(parent, out var definition)
            ? definition
            : throw new InvalidOperationException(
                $"{parent.Kind} '{parent.Id}' has no declared Add operation.");
}
