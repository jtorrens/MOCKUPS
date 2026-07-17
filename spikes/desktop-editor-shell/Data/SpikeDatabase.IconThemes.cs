using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public IReadOnlyList<FieldOption> GetIconThemeOptions(string projectId)
    {
        using var connection = OpenConnection();
        var options = _iconThemeRepository.QueryAll(connection)
            .Where((theme) => theme.ProjectId == projectId)
            .OrderBy((theme) => theme.Name)
            .Select((theme) => new FieldOption(theme.Id, theme.Name))
            .ToList();
        options.Insert(0, new FieldOption("", "None"));
        return options;
    }

    public IconThemeSettings GetIconThemeSettings(string iconThemeId)
    {
        var record = _iconThemeRepository.Get(iconThemeId);

        return new IconThemeSettings(
            record.Name,
            record.AssetRoot,
            record.MappingJson,
            record.MetadataJson);
    }

    public string GetIconThemeFieldValue(string iconThemeId, string fieldId)
    {
        var settings = GetIconThemeSettings(iconThemeId);
        return fieldId switch
        {
            "iconTheme.assetRoot" => settings.AssetRoot,
            "iconTheme.tokenCount" => IconThemeTokenCount(settings.MappingJson).ToString(),
            "iconTheme.metadata" => settings.MetadataJson,
            _ => throw new InvalidOperationException($"Unknown icon theme field '{fieldId}'."),
        };
    }

    public IReadOnlyList<IconThemeToken> GetIconThemeTokens(string iconThemeId)
    {
        var settings = GetIconThemeSettings(iconThemeId);
        return IconThemeTokens(settings.MappingJson);
    }

    public IReadOnlyList<FieldOption> GetIconTokenOptions(string projectId, string? currentToken = null)
    {
        using var connection = OpenConnection();
        var tokens = _iconThemeRepository.QueryAll(connection)
            .Where((row) => row.ProjectId == projectId)
            .SelectMany((row) => IconThemeTokens(row.MappingJson).Select((token) => token.Token))
            .ToHashSet(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(currentToken))
        {
            foreach (var token in currentToken.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                tokens.Add(token);
            }
        }

        return tokens
            .OrderBy((token) => token, StringComparer.Ordinal)
            .Select((token) => new FieldOption(token, token))
            .ToList();
    }

    public string ResolveIconThemeAssetPath(string iconThemeId, string file)
    {
        var settings = GetIconThemeSettings(iconThemeId);
        var projectId = ProjectIdForIconTheme(iconThemeId);
        var mediaRoot = ResolveProjectPath(GetProjectSettings(projectId).MediaRoot);
        return Path.Combine(mediaRoot, settings.AssetRoot, file);
    }

    public IconThemeRefreshResult RefreshIconThemeSets(ProjectTreeNode iconThemesRoot)
    {
        if (iconThemesRoot.Kind != ProjectTreeNodeKind.IconThemesRoot)
        {
            throw new InvalidOperationException("Icon themes can only be refreshed from the Icon Themes root.");
        }

        var project = ProjectAncestor(iconThemesRoot);
        using var connection = OpenConnection();
        return RefreshIconThemeSets(connection, project.Id);
    }

    public IconThemeRefreshResult RefreshIconThemeSetsForTheme(string iconThemeId)
    {
        using var connection = OpenConnection();
        return RefreshIconThemeSets(connection, ProjectIdForIconTheme(connection, iconThemeId));
    }

    public void DeleteIconThemeToken(string iconThemeId, string token)
    {
        if (!ValidIconTokenRegex().IsMatch(token))
        {
            throw new InvalidOperationException("Icon token must be lower_snake_case.");
        }

        using var connection = OpenConnection();
        var projectId = ProjectIdForIconTheme(connection, iconThemeId);
        var rows = _iconThemeRepository.QueryAll(connection).Where((row) => row.ProjectId == projectId).ToList();
        var mediaRoot = ResolveProjectPath(GetProjectSettings(projectId).MediaRoot);
        foreach (var row in rows)
        {
            var fullPath = Path.Combine(mediaRoot, row.AssetRoot, $"{token}.svg");
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        RefreshIconThemeSets(connection, projectId);
    }

    public IconThemeTokenSvg ReadIconThemeTokenSvg(string iconThemeId, string token)
    {
        using var connection = OpenConnection();
        var (row, file) = IconThemeTokenFile(connection, iconThemeId, token);
        var path = Path.Combine(IconThemeAssetDirectory(connection, row.ProjectId, row.AssetRoot), file);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Missing SVG file '{file}'.");
        }

        return new IconThemeTokenSvg(token, file, File.ReadAllText(path));
    }

    public IconThemeReplaceSvgResult ReplaceIconThemeTokenSvg(string iconThemeId, string token, string svgText)
    {
        svgText = SvgReplacementService.Validate(svgText);
        using var connection = OpenConnection();
        var (row, file) = IconThemeTokenFile(connection, iconThemeId, token);
        var targetDirectory = IconThemeAssetDirectory(connection, row.ProjectId, row.AssetRoot);
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllText(Path.Combine(targetDirectory, file), svgText);
        return new IconThemeReplaceSvgResult(token, file);
    }

    public IconThemeWriteAllSvgResult WriteIconThemeTokenSvgToAllSets(
        string iconThemeId,
        string token,
        string svgText,
        string description)
    {
        token = token.Trim();
        svgText = SvgReplacementService.Validate(svgText);
        if (!ValidIconTokenRegex().IsMatch(token))
        {
            throw new InvalidOperationException("Icon token must be lower_snake_case.");
        }

        using var connection = OpenConnection();
        var projectId = ProjectIdForIconTheme(connection, iconThemeId);
        var mediaRoot = ResolveProjectPath(GetProjectSettings(projectId).MediaRoot);
        var rows = _iconThemeRepository.QueryAll(connection).Where((row) => row.ProjectId == projectId).ToList();
        if (rows.Count == 0)
        {
            throw new InvalidOperationException("Refresh icon sets before saving tokens.");
        }

        foreach (var row in rows)
        {
            var targetDirectory = Path.Combine(mediaRoot, row.AssetRoot);
            Directory.CreateDirectory(targetDirectory);
            File.WriteAllText(Path.Combine(targetDirectory, $"{token}.svg"), svgText);
        }

        var refresh = RefreshIconThemeSets(connection, projectId);
        UpdateIconThemeTokenMetadata(
            connection,
            projectId,
            token,
            IconTokenCategory(token),
            description,
            "manual-svg-transform",
            "manual-svg-transform");
        return new IconThemeWriteAllSvgResult(token, rows.Count, refresh);
    }

    public IconThemeReplaceSvgResult ReplaceIconThemeTokenSvgFromFile(string iconThemeId, string token, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new InvalidOperationException("Select an existing SVG file.");
        }

        if (!Path.GetExtension(sourcePath).Equals(".svg", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only SVG files can be imported into an icon set.");
        }

        return ReplaceIconThemeTokenSvg(iconThemeId, token, File.ReadAllText(sourcePath));
    }

    private IconThemeRefreshResult RefreshIconThemeSets(SqliteConnection connection, string projectId)
    {
        var mediaRoot = ResolveProjectPath(GetProjectSettings(connection, projectId).MediaRoot);
        var iconThemesRoot = Path.Combine(mediaRoot, "icon-themes");
        Directory.CreateDirectory(iconThemesRoot);

        var setDirectories = Directory
            .EnumerateDirectories(iconThemesRoot)
            .Where((directory) => !Path.GetFileName(directory).StartsWith(".", StringComparison.Ordinal))
            .Where((directory) => !Path.GetFileName(directory).StartsWith("_", StringComparison.Ordinal))
            .OrderBy(Path.GetFileName)
            .ToList();

        foreach (var directory in setDirectories)
        {
            var setName = Path.GetFileName(directory);
            var id = $"icon_theme_{projectId}_{Slug(setName)}";
            var assetRoot = NormalizeRelativePath(Path.GetRelativePath(mediaRoot, directory));
            var metadata = IconThemeMetadata(directory, setName);
            _iconThemeRepository.UpsertDiscovered(
                connection,
                id,
                projectId,
                setName,
                assetRoot,
                metadata.ToJsonString());
        }

        var rows = _iconThemeRepository.QueryAll(connection).Where((row) => row.ProjectId == projectId).ToList();
        var tokensBySet = rows.ToDictionary(
            (row) => row.Id,
            (row) => IconTokenRules.SvgTokenSet(Path.Combine(mediaRoot, row.AssetRoot)));
        var commonTokens = tokensBySet.Values.FirstOrDefault()?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];
        foreach (var setTokens in tokensBySet.Values.Skip(1))
        {
            commonTokens.IntersectWith(setTokens);
        }

        var allTokens = tokensBySet.Values.SelectMany((set) => set).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var nextMapping = BuildIconThemeMapping(row.MappingJson, commonTokens);
            _iconThemeRepository.UpdateMapping(connection, row.Id, nextMapping.ToJsonString());
        }

        return new IconThemeRefreshResult(rows.Count, commonTokens.Count, Math.Max(0, allTokens.Count - commonTokens.Count));
    }

    private (IconThemeRecord Row, string File) IconThemeTokenFile(SqliteConnection connection, string iconThemeId, string token)
    {
        var row = _iconThemeRepository.Get(connection, iconThemeId);
        if (!ValidIconTokenRegex().IsMatch(token))
        {
            throw new InvalidOperationException("Icon token must be lower_snake_case.");
        }

        var mapping = ParseJsonObject(row.MappingJson);
        var tokens = mapping["tokens"] as JsonObject;
        var tokenObject = tokens?[token] as JsonObject
            ?? throw new InvalidOperationException($"Icon token '{token}' is not present in this icon set.");
        var file = JsonString(tokenObject, ["file"]);
        if (string.IsNullOrWhiteSpace(file))
        {
            throw new InvalidOperationException($"Icon token '{token}' has no explicit SVG file reference.");
        }
        if (!Path.GetExtension(file).Equals(".svg", StringComparison.OrdinalIgnoreCase) || Path.GetFileName(file) != file)
        {
            throw new InvalidOperationException($"Icon token '{token}' has an invalid SVG file reference.");
        }

        return (row, file);
    }

    private static JsonObject BuildIconThemeMapping(string currentMappingJson, HashSet<string> commonTokens)
    {
        return IconTokenRules.BuildMapping(currentMappingJson, commonTokens);
    }

    private static IReadOnlyList<IconThemeToken> IconThemeTokens(string mappingJson)
    {
        return IconTokenRules.Tokens(mappingJson)
            .Select((token) => new IconThemeToken(token.Token, token.Category, token.File, token.Description))
            .ToList();
    }

    private static int IconThemeTokenCount(string mappingJson)
    {
        return IconThemeTokens(mappingJson).Count;
    }

    private static string IconTokenCategory(string token)
    {
        return IconTokenRules.CategoryFromToken(token);
    }

    private string ProjectIdForIconTheme(SqliteConnection connection, string iconThemeId)
    {
        return _iconThemeRepository.Get(connection, iconThemeId).ProjectId;
    }

    private string ProjectIdForIconTheme(string iconThemeId)
    {
        using var connection = OpenConnection();
        return ProjectIdForIconTheme(connection, iconThemeId);
    }

    private static JsonObject IconThemeMetadata(string directory, string setName)
    {
        var manifestPath = Path.Combine(directory, "manifest.json");
        var metadata = new JsonObject
        {
            ["iconSet"] = IconSetDefinitionFromName(setName),
        };
        if (!File.Exists(manifestPath)) return metadata;

        try
        {
            var manifest = ParseJsonObject(File.ReadAllText(manifestPath));
            metadata["manifest"] = manifest.DeepClone();
            metadata["iconSet"] = IconSetDefinition(manifest, setName);
        }
        catch (InvalidOperationException)
        {
            // A malformed manifest should not block refreshing SVG tokens.
        }

        return metadata;
    }

    private static JsonObject IconSetDefinition(IconThemeRecord row)
    {
        var metadata = ParseJsonObject(row.MetadataJson);
        return metadata["iconSet"] is JsonObject iconSet
            ? (JsonObject)iconSet.DeepClone()
            : throw new InvalidOperationException($"Icon Theme '{row.Id}' metadata has no explicit iconSet contract.");
    }

    private static JsonObject IconSetDefinition(JsonObject manifest, string fallbackName)
    {
        var source = JsonString(manifest, ["source"]);
        var style = JsonString(manifest, ["style"]);
        var weight = JsonNumberString(manifest, ["weight"]);
        var manifestSetName = JsonString(manifest, ["name"]);
        if (string.IsNullOrWhiteSpace(manifestSetName))
        {
            manifestSetName = fallbackName;
        }

        if (source.Contains("lucide", StringComparison.OrdinalIgnoreCase) || style.Equals("lucide", StringComparison.OrdinalIgnoreCase))
        {
            return new JsonObject
            {
                ["provider"] = "lucide",
                ["setName"] = manifestSetName,
                ["package"] = string.IsNullOrWhiteSpace(source) ? "lucide-static" : source,
                ["stroke"] = JsonNumberDouble(manifest, ["stroke"], 2),
                ["fillMode"] = "stroke",
            };
        }

        return new JsonObject
        {
            ["provider"] = "material",
            ["setName"] = manifestSetName,
            ["package"] = string.IsNullOrWhiteSpace(source) ? "material-symbols" : source,
            ["style"] = string.IsNullOrWhiteSpace(style) ? "rounded" : style,
            ["weight"] = Math.Max(1, NumericText.Int32(weight, 400)),
            ["fillMode"] = "filled",
        };
    }

    private static JsonObject IconSetDefinitionFromName(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.Contains("lucide") || lower.Contains("lucida"))
        {
            return new JsonObject
            {
                ["provider"] = "lucide",
                ["setName"] = name,
                ["package"] = "lucide-static",
                ["stroke"] = 2,
                ["fillMode"] = "stroke",
            };
        }

        var style = lower.Contains("outlined") ? "outlined" : lower.Contains("sharp") ? "sharp" : "rounded";
        var weightMatch = Regex.Match(lower, "(100|200|300|400|500|600|700)");
        return new JsonObject
        {
            ["provider"] = "material",
            ["setName"] = name,
            ["package"] = "@material-symbols/svg-400",
            ["style"] = style,
            ["weight"] = weightMatch.Success ? int.Parse(weightMatch.Value) : 400,
            ["fillMode"] = "filled",
        };
    }

    [GeneratedRegex("^[a-z][a-z0-9_]*(?:\\.[a-z0-9_]+)*$")]
    private static partial Regex ValidIconTokenRegex();
}
