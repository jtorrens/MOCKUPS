using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.IO;

namespace Mockups.DesktopEditorShell;

internal sealed record DesktopApplicationServices(
    SpikeDatabase Database,
    EditorVariantHistoryService VariantHistory,
    CoreFieldValueService CoreFieldValues,
    RecordClassFieldValueService RecordClassFieldValues,
    ComponentClassFieldValueService ComponentClassFieldValues,
    ProductionShotContextService ProductionShotContext,
    EditorWorkspaceCoordinator WorkspaceCoordinator);

internal sealed class DesktopCompositionRoot
{
    public static string DefaultDatabasePath() =>
        SqlitePersistence.DefaultDatabasePath();

    public DesktopApplicationServices Create(string databasePath)
    {
        DesktopPreviewBundle.RequireCurrent(
            Path.Combine(AppContext.BaseDirectory, "desktop-preview"));
        var database = SqlitePersistence.OpenCurrent(databasePath);
        return new DesktopApplicationServices(
            database,
            new EditorVariantHistoryService(database),
            new CoreFieldValueService(database),
            new RecordClassFieldValueService(database),
            new ComponentClassFieldValueService(database),
            new ProductionShotContextService(new ProductionShotContextDataSource(database)),
            new EditorWorkspaceCoordinator(database));
    }
}
