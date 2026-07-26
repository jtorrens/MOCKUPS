using Mockups.DesktopEditorShell.EditorShell;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine : IEditorNavigationDataSource
{
    IReadOnlyList<ProjectTreeNode> IEditorNavigationDataSource.LoadProjectTree() =>
        LoadProjectTree();
}
