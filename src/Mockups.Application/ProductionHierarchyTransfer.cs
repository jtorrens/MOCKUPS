using System;

namespace Mockups.DesktopEditorShell.EditorShell;

public enum ProductionHierarchyTransferMode
{
    Copy,
    Move,
}

public static class ProductionHierarchyTransferContract
{
    public static bool CanTransfer(
        ProjectTreeNode source,
        ProjectTreeNode target)
    {
        if (source.Parent is null)
        {
            return false;
        }

        return source.Kind switch
        {
            ProjectTreeNodeKind.Shot =>
                target.Kind == ProjectTreeNodeKind.Episode
                && source.Parent.Id != target.Id
                && SameProject(source, target),
            ProjectTreeNodeKind.ModuleInstance =>
                target.Kind == ProjectTreeNodeKind.Shot
                && source.Parent.Id != target.Id
                && SameProject(source, target),
            _ => false,
        };
    }

    public static void RequireTransfer(
        ProjectTreeNode source,
        ProjectTreeNode target,
        ProductionHierarchyTransferMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new InvalidOperationException(
                $"Unknown Production transfer mode '{mode}'.");
        }
        if (!CanTransfer(source, target))
        {
            throw new InvalidOperationException(
                $"Cannot {mode.ToString().ToLowerInvariant()} {source.Kind} "
                + $"'{source.Id}' into {target.Kind} '{target.Id}'.");
        }
    }

    private static bool SameProject(
        ProjectTreeNode source,
        ProjectTreeNode target)
    {
        var sourceProject = ProjectAncestor(source);
        var targetProject = ProjectAncestor(target);
        return sourceProject is not null
            && targetProject is not null
            && sourceProject.Id.Equals(
                targetProject.Id,
                StringComparison.Ordinal);
    }

    private static ProjectTreeNode? ProjectAncestor(ProjectTreeNode node)
    {
        for (ProjectTreeNode? current = node;
             current is not null;
             current = current.Parent)
        {
            if (current.Kind == ProjectTreeNodeKind.Project)
            {
                return current;
            }
        }
        return null;
    }
}
