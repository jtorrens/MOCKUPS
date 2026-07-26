using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.EditorShell;

namespace Mockups.DesktopEditorShell;

internal sealed record DesktopApplicationServices(
    SpikeDatabase Database,
    EditorVariantHistoryService VariantHistory,
    CoreFieldValueService CoreFieldValues,
    RecordClassFieldValueService RecordClassFieldValues,
    ComponentClassFieldValueService ComponentClassFieldValues,
    ProductionShotContextService ProductionShotContext);

internal sealed class DesktopCompositionRoot
{
    public static string DefaultDatabasePath() =>
        SqlitePersistence.DefaultDatabasePath();

    public DesktopApplicationServices Create(string databasePath)
    {
        var database = SqlitePersistence.OpenCurrent(databasePath);
        return new DesktopApplicationServices(
            database,
            new EditorVariantHistoryService(database),
            new CoreFieldValueService(database),
            new RecordClassFieldValueService(database),
            new ComponentClassFieldValueService(database),
            new ProductionShotContextService(new ProductionShotContextDataSource(database)));
    }
}
