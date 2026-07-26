using Mockups.DesktopEditorShell.EditorShell;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

public sealed partial class SpikeDatabase : IEditorNavigationDataSource
{
    IReadOnlyList<ProjectTreeNode> IEditorNavigationDataSource.LoadProjectTree() =>
        LoadProjectTree();
}
