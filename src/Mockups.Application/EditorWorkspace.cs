using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

public enum EditorWorkspace
{
    Design,
    Production,
}

[Flags]
public enum EditorWorkspaceScope
{
    None = 0,
    Design = 1,
    Production = 2,
    Both = Design | Production,
}

public static class EditorWorkspacePolicy
{
    public static EditorWorkspaceScope Scope(ProjectTreeNodeKind kind)
    {
        return kind switch
        {
            ProjectTreeNodeKind.Project => EditorWorkspaceScope.Both,
            ProjectTreeNodeKind.AppsRoot or ProjectTreeNodeKind.App
                or ProjectTreeNodeKind.Module or ProjectTreeNodeKind.ModuleVariant =>
                EditorWorkspaceScope.Design,
            ProjectTreeNodeKind.PaletteRoot or ProjectTreeNodeKind.PaletteColor =>
                EditorWorkspaceScope.Design,
            ProjectTreeNodeKind.IconThemesRoot or ProjectTreeNodeKind.IconTheme =>
                EditorWorkspaceScope.Design,
            ProjectTreeNodeKind.ComponentClassesRoot
                or ProjectTreeNodeKind.ComponentClassGroup
                or ProjectTreeNodeKind.ComponentClass
                or ProjectTreeNodeKind.ComponentVariant =>
                EditorWorkspaceScope.Design,
            ProjectTreeNodeKind.ThemesRoot or ProjectTreeNodeKind.Theme =>
                EditorWorkspaceScope.Design,
            ProjectTreeNodeKind.EpisodesRoot or ProjectTreeNodeKind.Episode
                or ProjectTreeNodeKind.Shot or ProjectTreeNodeKind.ModuleInstance =>
                EditorWorkspaceScope.Production,
            ProjectTreeNodeKind.ProductionDataRoot
                or ProjectTreeNodeKind.RenderQueueRoot
                or ProjectTreeNodeKind.ActorsRoot or ProjectTreeNodeKind.Actor
                or ProjectTreeNodeKind.DevicesRoot or ProjectTreeNodeKind.Device
                or ProjectTreeNodeKind.ProductionFontsRoot
                or ProjectTreeNodeKind.ProductionFont =>
                EditorWorkspaceScope.Production,
            _ => EditorWorkspaceScope.None,
        };
    }

    public static bool IsSectionRoot(ProjectTreeNodeKind kind)
    {
        return kind is ProjectTreeNodeKind.AppsRoot
            or ProjectTreeNodeKind.ComponentClassesRoot
            or ProjectTreeNodeKind.ThemesRoot
            or ProjectTreeNodeKind.PaletteRoot
            or ProjectTreeNodeKind.IconThemesRoot
            or ProjectTreeNodeKind.EpisodesRoot
            or ProjectTreeNodeKind.RenderQueueRoot
            or ProjectTreeNodeKind.ProductionDataRoot;
    }

    public static int SectionOrder(ProjectTreeNodeKind kind)
    {
        return kind switch
        {
            ProjectTreeNodeKind.AppsRoot => 10,
            ProjectTreeNodeKind.ComponentClassesRoot => 20,
            ProjectTreeNodeKind.ThemesRoot => 30,
            ProjectTreeNodeKind.PaletteRoot => 40,
            ProjectTreeNodeKind.IconThemesRoot => 50,
            ProjectTreeNodeKind.EpisodesRoot => 10,
            ProjectTreeNodeKind.RenderQueueRoot => 20,
            ProjectTreeNodeKind.ProductionDataRoot => 30,
            _ => 100,
        };
    }
}

public static class EditorWorkspaceNavigation
{
    public static IReadOnlyList<ProjectTreeNode> SectionRoots(
        ProjectTreeNode project,
        EditorWorkspace workspace)
    {
        var roots = DescendantsAndSelf(project)
            .Where((node) => EditorWorkspacePolicy.IsSectionRoot(node.Kind))
            .Where((node) => Includes(EditorWorkspacePolicy.Scope(node.Kind), workspace))
            .ToList();
        if (workspace == EditorWorkspace.Production)
        {
            roots.Add(new ProjectTreeNode(
                ProjectTreeNodeKind.RenderQueueRoot,
                $"render-queue:{project.Id}",
                "Render Queue",
                "Local render jobs and history",
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.RenderQueueRoot),
                project));
        }
        return roots
            .OrderBy((node) => EditorWorkspacePolicy.SectionOrder(node.Kind))
            .ToList();
    }

    public static bool Contains(EditorWorkspace workspace, ProjectTreeNode node)
    {
        if (node.Kind == ProjectTreeNodeKind.Project) return true;
        return Includes(EditorWorkspacePolicy.Scope(node.Kind), workspace);
    }

    public static ProjectTreeNode? FirstSelectable(
        IReadOnlyList<ProjectTreeNode> treeRoots,
        EditorWorkspace workspace)
    {
        foreach (var project in treeRoots)
        {
            foreach (var root in SectionRoots(project, workspace))
            {
                var node = DescendantsAndSelf(root)
                    .FirstOrDefault(EditorNodeSelectionState.CanSelectTreeNode);
                if (node is not null) return node;
            }
        }

        return null;
    }

    public static string Title(EditorWorkspace workspace) =>
        workspace == EditorWorkspace.Design ? "Design" : "Production";

    public static EditorWorkspace Parse(string? value) =>
        string.Equals(value, "production", StringComparison.OrdinalIgnoreCase)
            ? EditorWorkspace.Production
            : EditorWorkspace.Design;

    public static string StorageValue(EditorWorkspace workspace) =>
        workspace == EditorWorkspace.Design ? "design" : "production";

    private static IEnumerable<ProjectTreeNode> DescendantsAndSelf(ProjectTreeNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in DescendantsAndSelf(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool Includes(
        EditorWorkspaceScope scope,
        EditorWorkspace workspace)
    {
        var target = workspace == EditorWorkspace.Design
            ? EditorWorkspaceScope.Design
            : EditorWorkspaceScope.Production;
        return (scope & target) != 0;
    }
}
