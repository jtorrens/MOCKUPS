using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EditorEmbeddedContext(
    ProjectTreeNode OwnerNode,
    IReadOnlyList<EmbeddedComponentSlotDefinition> Slots)
{
    public EmbeddedComponentSlotDefinition Slot => Slots[^1];
}
