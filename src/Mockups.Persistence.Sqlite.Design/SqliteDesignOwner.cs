using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    private object WriteGate => _context.WriteGate;
    private readonly SqliteProjectContext _context;
    private readonly IAppModuleRepository _appModuleRepository;
    private readonly IComponentClassRepository _componentClassRepository;

    internal SqliteDesignOwner(SqliteProjectContext context)
    {
        _context = context;
        _appModuleRepository = new AppModuleRepository(context);
        _componentClassRepository = new ComponentClassRepository(context);
    }

    internal IAppModuleRepository AppModuleRepository =>
        _appModuleRepository;

    internal IComponentClassRepository ComponentClassRepository =>
        _componentClassRepository;

    private SqliteConnection OpenConnection() => _context.OpenConnection();

    private static JsonObject ParseJsonObject(string json) =>
        JsonPath.ParseRequiredObject(json, "Current persisted JSON object");

    private static string JsonString(
        JsonObject root,
        IReadOnlyList<string> path) =>
        JsonPath.String(root, path);

    private static bool JsonBool(
        JsonObject root,
        IReadOnlyList<string> path) =>
        JsonPath.Bool(root, path);

    private static void SetPair(
        JsonObject root,
        string pairValue,
        IReadOnlyList<string> firstPath,
        IReadOnlyList<string> secondPath,
        bool asNumber = true) =>
        JsonPath.SetPair(
            root,
            pairValue,
            firstPath,
            secondPath,
            asNumber);

    private static void SetJsonValue(
        JsonObject root,
        IReadOnlyList<string> path,
        JsonNode value) =>
        JsonPath.Set(root, path, value);

    private static JsonNode NumberNode(string value) =>
        JsonPath.NumberNode(value);
}
