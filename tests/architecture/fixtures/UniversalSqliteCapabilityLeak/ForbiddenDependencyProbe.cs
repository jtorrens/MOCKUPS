using Mockups.DesktopEditorShell.Data;

namespace UniversalSqliteCapabilityLeak;

public static class ForbiddenDependencyProbe
{
    public static object Read(
        SqliteProjectSession session) =>
        session.GetProjectSettings("project");
}
