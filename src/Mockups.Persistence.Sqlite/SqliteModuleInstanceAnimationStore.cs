using Mockups.DesktopEditorShell.Common;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteModuleInstanceAnimationStore(
    SqliteProjectContext context,
    SqliteProductionOwner production)
    : IModuleInstanceAnimationStore
{
    public void UpdateModuleInstanceAnimationJson(string moduleInstanceId, string animationJson)
    {
        using var connection = context.OpenConnection();
        production.UpdateModuleInstanceAnimationJson(connection, moduleInstanceId, animationJson);
    }
}
