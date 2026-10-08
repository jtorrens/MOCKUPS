using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;

public static class ForbiddenDependencyProbe
{
    public static void Write(IRuntimeInputOwnerStore store, RuntimeComponentOverrideSource source)
    {
        store.UpdateModuleDesignPreviewJson("module", "{}");
        store.UpdateComponentClassDesignPreviewJson("component", "{}");
        source.Overrides.Clear();
    }
}
