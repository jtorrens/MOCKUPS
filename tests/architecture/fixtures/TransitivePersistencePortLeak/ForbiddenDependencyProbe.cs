using Mockups.DesktopEditorShell.Data;

namespace TransitivePersistencePortLeak;

public static class ForbiddenDependencyProbe
{
    public static object Read(IRecordClassFieldStore store) =>
        store;
}
