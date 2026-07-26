using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public EditorLayout LoadEditorLayout(string recordClassId)
    {
        return _editorLayoutRepository.Load(recordClassId);
    }

    public void SaveEditorLayout(string recordClassId, EditorLayout layout)
    {
        _editorLayoutRepository.Save(recordClassId, layout);
    }
}
