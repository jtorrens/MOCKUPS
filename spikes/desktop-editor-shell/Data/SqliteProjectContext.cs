using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.IO;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteProjectContext
{
    private readonly string _connectionString;
    private readonly string _validationConnectionString;

    public SqliteProjectContext(string databasePath)
    {
        DatabasePath = Path.GetFullPath(databasePath);
        ProjectPathService.ConfigureProjectRoot(ProjectRootForDatabase(DatabasePath));
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            ForeignKeys = true,
        }.ToString();
        _validationConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            ForeignKeys = true,
            Mode = SqliteOpenMode.ReadOnly,
        }.ToString();
    }

    public static object WriteGate { get; } = new();

    public string DatabasePath { get; }

    public SqliteConnection OpenConnection()
    {
        return Open(_connectionString);
    }

    public SqliteConnection OpenValidationConnection()
    {
        return Open(_validationConnectionString);
    }

    private static SqliteConnection Open(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
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
