using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private SqliteConnection OpenConnection()
    {
        return _context.OpenConnection();
    }

    private SqliteConnection OpenValidationConnection()
    {
        return _context.OpenValidationConnection();
    }


    private static int NextSortOrder(SqliteConnection connection, string table, string parentColumn, string parentId)
    {
        return SqliteCommandExecutor.NextSortOrder(connection, table, parentColumn, parentId);
    }

    private static string ReadString(SqliteDataReader reader, int index)
    {
        return SqliteCommandExecutor.ReadString(reader, index);
    }

    private static string Slug(string value)
    {
        return SlugText.LowerSnake(value, "font");
    }

    private static string SlugOrName(string slug, string name, string fallback)
    {
        return SlugText.LowerSnakeOrName(slug, name, fallback);
    }

    private static string ResolveProjectPath(string path)
    {
        return ProjectPathService.ResolveProjectPath(path);
    }

    private static string NormalizeRelativePath(string path)
    {
        return ProjectPathService.NormalizeRelativePath(path);
    }

    private static string MetadataString(string metadataJson, string key)
    {
        var metadata = ParseJsonObject(metadataJson);
        return JsonString(metadata, [key]);
    }

    private static bool MetadataBool(string metadataJson, string key)
    {
        var metadata = ParseJsonObject(metadataJson);
        if (metadata[key] is not JsonValue value)
        {
            return false;
        }

        if (value.TryGetValue<bool>(out var boolean))
        {
            return boolean;
        }

        if (value.TryGetValue<string>(out var text)) return StringToBool(text);
        return value.TryGetValue<int>(out var number) && number != 0;
    }

    private static bool StringToBool(string value)
    {
        return BooleanText.Parse(value);
    }

    private static string BoolToString(bool value)
    {
        return BooleanText.Format(value);
    }

    private static JsonObject ParseJsonObject(string json)
    {
        return JsonPath.ParseRequiredObject(json, "Current persisted JSON object");
    }

    private static string MetricPair(string metricsJson, IReadOnlyList<string> firstPath, IReadOnlyList<string> secondPath)
    {
        return JsonPath.Pair(ParseJsonObject(metricsJson), firstPath, secondPath);
    }

    private static string JsonNumberString(JsonObject root, IReadOnlyList<string> path)
    {
        return JsonPath.NumberString(root, path);
    }

    private static string JsonNumberString(JsonObject root, IReadOnlyList<string> path, string fallback)
    {
        return JsonPath.NumberString(root, path, fallback);
    }

    private static double JsonNumberDouble(JsonObject root, IReadOnlyList<string> path, double fallback)
    {
        return JsonPath.NumberDouble(root, path, fallback);
    }

    private static string JsonString(JsonObject root, IReadOnlyList<string> path)
    {
        return JsonPath.String(root, path);
    }

    private static bool JsonBool(JsonObject root, IReadOnlyList<string> path)
    {
        return JsonPath.Bool(root, path);
    }

    private static JsonNode? GetJsonValue(JsonObject root, IReadOnlyList<string> path)
    {
        return JsonPath.Get(root, path);
    }

    private static void SetPair(
        JsonObject root,
        string pairValue,
        IReadOnlyList<string> firstPath,
        IReadOnlyList<string> secondPath,
        bool asNumber = true)
    {
        JsonPath.SetPair(root, pairValue, firstPath, secondPath, asNumber);
    }

    private static void SetJsonValue(JsonObject root, IReadOnlyList<string> path, JsonNode value)
    {
        JsonPath.Set(root, path, value);
    }

    private static bool RemoveJsonValue(JsonObject root, IReadOnlyList<string> path)
    {
        return JsonPath.Remove(root, path);
    }

    private static JsonNode NumberNode(string value)
    {
        return JsonPath.NumberNode(value);
    }

    private static void ExecuteScript(SqliteConnection connection, string script)
    {
        SqliteCommandExecutor.ExecuteScript(connection, script);
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Key, object? Value)[] parameters)
    {
        Execute(connection, transaction: null, sql, parameters);
    }

    private static void Execute(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        params (string Key, object? Value)[] parameters)
    {
        SqliteCommandExecutor.Execute(connection, transaction, sql, parameters);
    }

    private static long ScalarLong(SqliteConnection connection, string sql, params (string Key, object? Value)[] parameters)
    {
        return SqliteCommandExecutor.ScalarLong(connection, sql, parameters);
    }

    private static string? ScalarString(SqliteConnection connection, string sql, params (string Key, object? Value)[] parameters)
    {
        return SqliteCommandExecutor.ScalarString(connection, sql, parameters);
    }
}
