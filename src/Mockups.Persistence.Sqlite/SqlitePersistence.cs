using Microsoft.Data.Sqlite;
using System.IO;

namespace Mockups.DesktopEditorShell.Data;

public sealed class CurrentDatabaseException(
    string databasePath,
    string message,
    Exception innerException)
    : InvalidOperationException(message, innerException)
{
    public string DatabasePath { get; } = databasePath;
}

public static class SqlitePersistence
{
    public static SqliteProjectSession OpenCurrent(
        string databasePath)
    {
        try
        {
            var context = new SqliteProjectContext(databasePath);
            return SqliteProjectSessionFactory.Create(context);
        }
        catch (FileNotFoundException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is InvalidOperationException
                or InvalidDataException
                or SqliteException)
        {
            throw new CurrentDatabaseException(
                Path.GetFullPath(databasePath),
                exception.Message,
                exception);
        }
    }

    public static string DefaultDatabasePath()
    {
        return Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "MOCKUPS",
            "mockups.sqlite");
    }
}
