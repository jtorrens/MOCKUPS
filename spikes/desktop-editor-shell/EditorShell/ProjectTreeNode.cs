using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal enum ProjectTreeNodeKind
{
    Project,
    ProductionDataRoot,
    SystemDataRoot,
    AppsRoot,
    PaletteRoot,
    IconThemesRoot,
    RenderPresetsRoot,
    ComponentClassesRoot,
    ComponentClassGroup,
    DevicesRoot,
    ActorsRoot,
    ThemesRoot,
    ProductionFontsRoot,
    EpisodesRoot,
    App,
    Module,
    Episode,
    Shot,
    ModuleInstance,
    PaletteColor,
    IconTheme,
    RenderPreset,
    ComponentClass,
    ComponentPreset,
    Device,
    Actor,
    Theme,
    ProductionFont,
}

internal sealed class ProjectTreeNode
{
    public ProjectTreeNode(
        ProjectTreeNodeKind kind,
        string id,
        string name,
        string notes,
        string recordClassId,
        ProjectTreeNode? parent = null,
        string? colorHex = null,
        bool isUsed = false,
        bool isProtected = false,
        bool isLocked = false)
    {
        Kind = kind;
        Id = id;
        Name = name;
        Notes = notes;
        RecordClassId = recordClassId;
        Parent = parent;
        ColorHex = colorHex;
        IsUsed = isUsed;
        IsProtected = isProtected;
        IsLocked = isLocked;
    }

    public ProjectTreeNodeKind Kind { get; }
    public string Id { get; }
    public string Name { get; set; }
    public string Notes { get; set; }
    public string RecordClassId { get; }
    public ProjectTreeNode? Parent { get; private set; }
    public string? ColorHex { get; set; }
    public bool IsUsed { get; }
    public bool IsProtected { get; }
    public bool IsLocked { get; }
    public List<ProjectTreeNode> Children { get; } = [];

    public int Level => Parent is null ? 0 : Parent.Level + 1;
    public bool CanAddChild => Kind is ProjectTreeNodeKind.AppsRoot
        or ProjectTreeNodeKind.App
        or ProjectTreeNodeKind.PaletteRoot
        or ProjectTreeNodeKind.IconThemesRoot
        or ProjectTreeNodeKind.RenderPresetsRoot
        or ProjectTreeNodeKind.DevicesRoot
        or ProjectTreeNodeKind.ActorsRoot
        or ProjectTreeNodeKind.ThemesRoot
        or ProjectTreeNodeKind.ProductionFontsRoot
        or ProjectTreeNodeKind.EpisodesRoot
        or ProjectTreeNodeKind.Episode;
    public bool CanDuplicate => Kind is ProjectTreeNodeKind.App
        or ProjectTreeNodeKind.Module
        or ProjectTreeNodeKind.Episode
        or ProjectTreeNodeKind.Shot
        or ProjectTreeNodeKind.PaletteColor
        or ProjectTreeNodeKind.IconTheme
        or ProjectTreeNodeKind.RenderPreset
        or ProjectTreeNodeKind.ComponentPreset
        or ProjectTreeNodeKind.Device
        or ProjectTreeNodeKind.Actor
        or ProjectTreeNodeKind.Theme;
    public bool CanRenameDirectly => Kind == ProjectTreeNodeKind.ComponentClass
        || (Kind == ProjectTreeNodeKind.ComponentPreset && !IsProtected);
    public bool CanDelete => Kind is ProjectTreeNodeKind.App
        or ProjectTreeNodeKind.Module
        or ProjectTreeNodeKind.Episode
        or ProjectTreeNodeKind.Shot
        or ProjectTreeNodeKind.PaletteColor
        or ProjectTreeNodeKind.IconTheme
        or ProjectTreeNodeKind.RenderPreset
        or ProjectTreeNodeKind.Device
        or ProjectTreeNodeKind.Actor
        or ProjectTreeNodeKind.Theme
        or ProjectTreeNodeKind.ProductionFont
        || (Kind == ProjectTreeNodeKind.ComponentPreset && !IsProtected && !IsLocked);
    public bool CanOpenEditor => Kind is not ProjectTreeNodeKind.ProductionDataRoot
        and not ProjectTreeNodeKind.SystemDataRoot
        and not ProjectTreeNodeKind.AppsRoot
        and not ProjectTreeNodeKind.PaletteRoot
        and not ProjectTreeNodeKind.IconThemesRoot
        and not ProjectTreeNodeKind.RenderPresetsRoot
        and not ProjectTreeNodeKind.ComponentClassesRoot
        and not ProjectTreeNodeKind.ComponentClassGroup
        and not ProjectTreeNodeKind.DevicesRoot
        and not ProjectTreeNodeKind.ActorsRoot
        and not ProjectTreeNodeKind.ThemesRoot
        and not ProjectTreeNodeKind.ProductionFontsRoot
        and not ProjectTreeNodeKind.EpisodesRoot
        and not ProjectTreeNodeKind.ModuleInstance
        and not ProjectTreeNodeKind.ComponentPreset;

    public string Display => Name;

    public void AddChild(ProjectTreeNode child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public static string DefaultRecordClassId(ProjectTreeNodeKind kind)
    {
        return kind switch
        {
            ProjectTreeNodeKind.Project => "project",
            ProjectTreeNodeKind.ProductionDataRoot => "navigation.production_data",
            ProjectTreeNodeKind.SystemDataRoot => "navigation.system_data",
            ProjectTreeNodeKind.AppsRoot => "navigation.apps",
            ProjectTreeNodeKind.PaletteRoot => "navigation.palette",
            ProjectTreeNodeKind.IconThemesRoot => "navigation.icon_themes",
            ProjectTreeNodeKind.RenderPresetsRoot => "navigation.render_presets",
            ProjectTreeNodeKind.ComponentClassesRoot => "navigation.component_classes",
            ProjectTreeNodeKind.ComponentClassGroup => "navigation.component_class_group",
            ProjectTreeNodeKind.DevicesRoot => "navigation.devices",
            ProjectTreeNodeKind.ActorsRoot => "navigation.actors",
            ProjectTreeNodeKind.ThemesRoot => "navigation.themes",
            ProjectTreeNodeKind.ProductionFontsRoot => "navigation.production_fonts",
            ProjectTreeNodeKind.EpisodesRoot => "navigation.episodes",
            ProjectTreeNodeKind.App => "app.generic",
            ProjectTreeNodeKind.Module => "module.generic",
            ProjectTreeNodeKind.Episode => "episode",
            ProjectTreeNodeKind.Shot => "shot",
            ProjectTreeNodeKind.ModuleInstance => "module_instance",
            ProjectTreeNodeKind.PaletteColor => "palette_color",
            ProjectTreeNodeKind.IconTheme => "icon_theme",
            ProjectTreeNodeKind.RenderPreset => "render_preset",
            ProjectTreeNodeKind.ComponentClass => "component.avatar",
            ProjectTreeNodeKind.ComponentPreset => "component.preset",
            ProjectTreeNodeKind.Device => "device",
            ProjectTreeNodeKind.Actor => "actor",
            ProjectTreeNodeKind.Theme => "theme",
            ProjectTreeNodeKind.ProductionFont => "production_font",
            _ => throw new InvalidOperationException($"No record class for {kind}."),
        };
    }
}
