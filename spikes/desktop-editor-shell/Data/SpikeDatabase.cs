using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static readonly object WriteGate = new();
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly string _validationConnectionString;

    public SpikeDatabase(string databasePath)
    {
        databasePath = Path.GetFullPath(databasePath);
        _databasePath = databasePath;
        ProjectPathService.ConfigureProjectRoot(ProjectRootForDatabase(databasePath));
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
        }.ToString();
        _validationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();

        Initialize();
    }

    public static string DefaultDatabasePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "package.json"))
                && Directory.Exists(Path.Combine(directory.FullName, "assets")))
            {
                return Path.Combine(directory.FullName, "data", "desktop-editor-spike.sqlite");
            }

            directory = directory.Parent;
        }

        var root = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, "..", "..", "..", "..", "..", "data", "desktop-editor-spike.sqlite"));
    }

    private void Initialize()
    {
        if (!File.Exists(_databasePath))
        {
            throw new FileNotFoundException(
                "Desktop database does not exist. Create a validated database explicitly before opening the application.",
                _databasePath);
        }

        using var validationConnection = OpenValidationConnection();
        if (!HasUserTables(validationConnection))
        {
            throw new InvalidOperationException(
                $"Desktop database '{_databasePath}' is empty. Create a validated database explicitly before opening the application.");
        }

        ValidateSchemaV1(validationConnection);
    }

    private static bool HasUserTables(SqliteConnection connection)
    {
        return ScalarLong(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'") > 0;
    }

    private static string ProjectRootForDatabase(string databasePath)
    {
        var databaseDirectory = Path.GetDirectoryName(databasePath) ?? AppContext.BaseDirectory;
        var directoryName = Path.GetFileName(databaseDirectory);
        if (string.Equals(directoryName, "data", StringComparison.OrdinalIgnoreCase))
        {
            return Directory.GetParent(databaseDirectory)?.FullName ?? databaseDirectory;
        }

        return databaseDirectory;
    }
}
