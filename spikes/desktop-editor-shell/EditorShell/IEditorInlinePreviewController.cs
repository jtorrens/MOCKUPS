using Avalonia.Controls;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal interface IEditorInlinePreviewController
{
    void Reset();

    void AddIfNeeded(ProjectTreeNode node, EditorLayoutCard layoutCard, Panel groupPanel);

    void Refresh(ProjectTreeNode node, IReadOnlyDictionary<string, DictionaryFieldControl> controlsByFieldId);

    string ToStoragePath(ProjectTreeNode node, string fieldId, string path);
}
