using Microsoft.Data.Sqlite;
using System;

namespace Mockups.DesktopEditorShell.Data;

internal static class SqliteCommandExecutor
{
    public static int NextSortOrder(SqliteConnection connection, string table, string parentColumn, string parentId)
    {
        return (int)ScalarLong(
            connection,
            $"SELECT COALESCE(MAX(sort_order), -1) + 1 FROM {table} WHERE {parentColumn} = $parentId",
            ("$parentId", parentId));
    }

    public static string ReadString(SqliteDataReader reader, int index)
    {
        return reader.IsDBNull(index) ? "" : reader.GetString(index);
    }

    public static void ExecuteScript(SqliteConnection connection, string script)
    {
        using var command = connection.CreateCommand();
        command.CommandText = script;
        lock (SqliteProjectContext.WriteGate)
        {
            command.ExecuteNonQuery();
        }
    }

    public static void Execute(
        SqliteConnection connection,
        string sql,
        params (string Key, object? Value)[] parameters)
    {
        Execute(connection, transaction: null, sql, parameters);
    }

    public static void Execute(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        params (string Key, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (key, value) in parameters)
        {
            command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        }

        lock (SqliteProjectContext.WriteGate)
        {
            command.ExecuteNonQuery();
        }
    }

    public static long ScalarLong(
        SqliteConnection connection,
        string sql,
        params (string Key, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (key, value) in parameters)
        {
            command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        }

        return Convert.ToInt64(command.ExecuteScalar());
    }

    public static string? ScalarString(
        SqliteConnection connection,
        string sql,
        params (string Key, object? Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (key, value) in parameters)
        {
            command.Parameters.AddWithValue(key, value ?? DBNull.Value);
        }

        return command.ExecuteScalar() as string;
    }
}
