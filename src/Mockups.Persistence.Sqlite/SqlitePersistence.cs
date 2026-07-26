namespace Mockups.DesktopEditorShell.Data;

public static class SqlitePersistence
{
    public static SpikeDatabase OpenCurrent(string databasePath) =>
        new(databasePath);

    public static string DefaultDatabasePath() =>
        SpikeDatabase.DefaultDatabasePath();
}
