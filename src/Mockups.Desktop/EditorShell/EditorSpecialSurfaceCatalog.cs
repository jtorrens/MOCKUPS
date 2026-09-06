using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorSpecialSurfaceCatalog(
    params Func<ProjectTreeNode, IReadOnlyList<InstantEditorCard>?>[] owners)
{
    public IReadOnlyList<InstantEditorCard>? CreateCards(ProjectTreeNode node)
    {
        var matches = owners
            .Select((owner) => owner(node))
            .Where((cards) => cards is not null)
            .ToArray();
        return matches.Length switch
        {
            0 => null,
            1 => matches[0],
            _ => throw new InvalidOperationException(
                $"More than one special editor surface owns '{node.Kind}:{node.Id}'."),
        };
    }
}
