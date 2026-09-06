using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace Mockups.DesktopEditorShell.Data;

public sealed record SqliteDatabaseSnapshot(
    string Path,
    int SchemaVersion);

public static class SqliteDatabaseSnapshotService
{
    public static SqliteDatabaseSnapshot CreateValidated(
        string sourcePath,
        string destinationPath)
    {
        var source = Path.GetFullPath(sourcePath);
        var destination = Path.GetFullPath(destinationPath);
        if (string.Equals(
                source,
                destination,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "A SQLite snapshot destination must differ from its source.");
        }
        if (!File.Exists(source))
        {
            throw new FileNotFoundException(
                "The current MOCKUPS database does not exist.",
                source);
        }
        if (File.Exists(destination))
        {
            throw new InvalidOperationException(
                $"The SQLite snapshot destination already exists: {destination}");
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(destination)!);
        try
        {
            using (var sourceConnection = Open(
                       source,
                       SqliteOpenMode.ReadOnly))
            using (var destinationConnection = Open(
                       destination,
                       SqliteOpenMode.ReadWriteCreate))
            {
                sourceConnection.BackupDatabase(
                    destinationConnection);
            }

            FlushFile(destination);
            return Validate(destination);
        }
        catch
        {
            File.Delete(destination);
            throw;
        }
    }

    public static SqliteDatabaseSnapshot Validate(
        string snapshotPath)
    {
        var path = Path.GetFullPath(snapshotPath);
        _ = SqlitePersistence.OpenCurrent(path);

        using var connection = Open(
            path,
            SqliteOpenMode.ReadOnly);
        using var integrity = connection.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check";
        var integrityResult = integrity.ExecuteScalar() as string;
        if (!string.Equals(
                integrityResult,
                "ok",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"The SQLite snapshot failed integrity_check: {integrityResult ?? "no result"}");
        }

        using var schema = connection.CreateCommand();
        schema.CommandText = "PRAGMA user_version";
        var schemaVersion = Convert.ToInt32(
            schema.ExecuteScalar());
        return new SqliteDatabaseSnapshot(
            path,
            schemaVersion);
    }

    private static SqliteConnection Open(
        string path,
        SqliteOpenMode mode)
    {
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = path,
                ForeignKeys = true,
                Mode = mode,
            }.ToString());
        connection.Open();
        return connection;
    }

    private static void FlushFile(string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.ReadWrite,
            FileShare.Read);
        stream.Flush(flushToDisk: true);
    }
}
