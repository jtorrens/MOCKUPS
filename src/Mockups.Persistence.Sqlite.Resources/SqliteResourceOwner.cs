using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed record IconThemeAssetMoveResult(
    string AssetRoot,
    string Name);

internal sealed partial class SqliteResourceOwner :
    IActorPreviewRepository,
    IEditorPresentationContextRepository,
    IThemeTokenQuery,
    IModuleInstanceThemeTokenQuery,
    IIconThemeAssetStore,
    IComponentFieldResourceOptionSource
{
    private readonly SqliteProjectContext _context;
    private readonly IProjectEpisodeRepository _projectEpisodeRepository;
    private readonly IModuleInstanceThemeContextService _moduleInstanceThemeContextService;
    private readonly IPaletteRepository _paletteRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly IActorRepository _actorRepository;
    private readonly IThemeRepository _themeRepository;
    private readonly IProductionFontRepository _productionFontRepository;
    private readonly IIconThemeRepository _iconThemeRepository;

    internal SqliteResourceOwner(
        SqliteProjectContext context,
        IProjectEpisodeRepository projectEpisodeRepository,
        IModuleInstanceThemeContextService moduleInstanceThemeContextService)
    {
        _context = context;
        _projectEpisodeRepository = projectEpisodeRepository;
        _moduleInstanceThemeContextService = moduleInstanceThemeContextService;
        _paletteRepository = new PaletteRepository(context);
        _deviceRepository = new DeviceRepository(context);
        _actorRepository = new ActorRepository(context);
        _themeRepository = new ThemeRepository(context);
        _productionFontRepository = new ProductionFontRepository(context);
        _iconThemeRepository = new IconThemeRepository(context);
    }

    internal IPaletteRepository PaletteRepository => _paletteRepository;

    internal IDeviceRepository DeviceRepository => _deviceRepository;

    internal IActorRepository ActorRepository => _actorRepository;

    internal IThemeRepository ThemeRepository => _themeRepository;

    internal IProductionFontRepository ProductionFontRepository =>
        _productionFontRepository;

    internal IIconThemeRepository IconThemeRepository => _iconThemeRepository;

    private SqliteConnection OpenConnection() => _context.OpenConnection();

    private string ResolveProjectPath(string path) =>
        _context.ProjectPaths.ResolveProjectPath(path);

    private string NormalizeRelativePath(string path) =>
        _context.ProjectPaths.NormalizeRelativePath(path);

    public ProjectSettings GetProjectSettings(string projectId) =>
        _projectEpisodeRepository.GetProjectSettings(projectId);

    private ProjectSettings GetProjectSettings(
        SqliteConnection connection,
        string projectId) =>
        _projectEpisodeRepository.GetProjectSettings(connection, projectId);

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent
                ?? throw new InvalidOperationException(
                    $"{node.Kind} has no project ancestor.");
        }

        return current;
    }

    private static string Slug(string value) =>
        SlugText.LowerSnake(value, "font");

    private static JsonObject ParseJsonObject(string json) =>
        JsonPath.ParseRequiredObject(json, "Current persisted JSON object");

    private static string RequiredNumberPair(
        string json,
        IReadOnlyList<string> firstPath,
        IReadOnlyList<string> secondPath,
        string context) =>
        JsonPath.RequiredNumberPair(
            ParseJsonObject(json),
            firstPath,
            secondPath,
            context);

    private static string RequiredStringPair(
        string json,
        IReadOnlyList<string> firstPath,
        IReadOnlyList<string> secondPath,
        string context) =>
        JsonPath.RequiredStringPair(
            ParseJsonObject(json),
            firstPath,
            secondPath,
            context);

    private static string JsonNumberString(
        JsonObject root,
        IReadOnlyList<string> path) =>
        JsonPath.NumberString(root, path);

    private static double JsonNumberDouble(
        JsonObject root,
        IReadOnlyList<string> path,
        double fallback) =>
        JsonPath.NumberDouble(root, path, fallback);

    private static string JsonString(
        JsonObject root,
        IReadOnlyList<string> path) =>
        JsonPath.String(root, path);

    private static JsonNode? GetJsonValue(
        JsonObject root,
        IReadOnlyList<string> path) =>
        JsonPath.Get(root, path);

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
