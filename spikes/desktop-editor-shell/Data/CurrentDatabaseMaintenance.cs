using Microsoft.Data.Sqlite;
using System;
using System.IO;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal static class CurrentDatabaseMaintenance
{
    public static bool TryRun(string[] args)
    {
        if (args.Contains("--validate-current-database", StringComparer.Ordinal))
        {
            var path = OptionValue(args, "--source") ?? SpikeDatabase.DefaultDatabasePath();
            _ = new SpikeDatabase(path);
            Console.WriteLine($"Current desktop database validated read-only with SQLite {RuntimeVersion(path)}: {Path.GetFullPath(path)}");
            return true;
        }

        if (!args.Contains("--create-current-database", StringComparer.Ordinal)) return false;

        var sourcePath = Path.GetFullPath(OptionValue(args, "--source") ?? SpikeDatabase.DefaultDatabasePath());
        var outputPath = Path.GetFullPath(OptionValue(args, "--output")
            ?? throw new InvalidOperationException("Current database creation requires an explicit --output path."));
        if (sourcePath.Equals(outputPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Current database source and output paths must differ.");
        }
        if (File.Exists(outputPath))
        {
            throw new InvalidOperationException($"Output already exists and will not be overwritten: {outputPath}");
        }

        _ = new SpikeDatabase(sourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        try
        {
            File.Copy(sourcePath, outputPath, overwrite: false);
            _ = new SpikeDatabase(outputPath);
        }
        catch
        {
            if (File.Exists(outputPath)) File.Delete(outputPath);
            throw;
        }

        Console.WriteLine($"Validated current desktop database copy created: {outputPath}");
        return true;
    }

    private static string RuntimeVersion(string databasePath)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            ForeignKeys = true,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT sqlite_version()";
        return command.ExecuteScalar() as string ?? "unknown";
    }

    private static string? OptionValue(string[] args, string option)
    {
        var index = Array.FindIndex(args, (value) => string.Equals(value, option, StringComparison.Ordinal));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
