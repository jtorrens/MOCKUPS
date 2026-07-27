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
            return new SqliteProjectSession(
                new SqliteProjectEngine(context),
                new SqliteEditorLayoutStore(context));
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
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(
                    Path.Combine(
                        directory.FullName,
                        "package.json"))
                && Directory.Exists(
                    Path.Combine(
                        directory.FullName,
                        "assets")))
            {
                return Path.Combine(
                    directory.FullName,
                    "data",
                    "desktop-editor-spike.sqlite");
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "..",
                "data",
                "desktop-editor-spike.sqlite"));
    }
}
