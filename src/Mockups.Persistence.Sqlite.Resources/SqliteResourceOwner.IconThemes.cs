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

internal sealed partial class SqliteResourceOwner
{
    public IReadOnlyList<FieldOption> GetIconThemeOptions(string projectId)
    {
        using var connection = OpenConnection();
        var options = _iconThemeRepository.QueryAll(connection)
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
        return Path.Combine(ResolveIconThemeAssetDirectory(iconThemeId), file);
    }

    public string ResolveIconThemeAssetDirectory(string iconThemeId)
    {
        var settings = GetIconThemeSettings(iconThemeId);
        return IconThemeAssetDirectory(settings.AssetRoot);
    }

    public IconThemeRefreshResult RefreshIconThemeSets(ProjectTreeNode iconThemesRoot)
    {
        if (iconThemesRoot.Kind != ProjectTreeNodeKind.IconThemesRoot)
        {
            throw new InvalidOperationException("Icon themes can only be refreshed from the Icon Themes root.");
        }

        using var connection = OpenConnection();
        return RefreshIconThemeSets(connection);
    }

    public IconThemeRefreshResult RefreshIconThemeSetsForTheme(string iconThemeId)
    {
        using var connection = OpenConnection();
        _ = _iconThemeRepository.Get(connection, iconThemeId);
        return RefreshIconThemeSets(connection);
    }

    public ResourceAssetDeletionResult DeleteIconThemeToken(string iconThemeId, string token)
    {
        if (!ValidIconTokenRegex().IsMatch(token))
        {
            throw new InvalidOperationException("Icon token must be lower_snake_case.");
        }

        lock (_context.WriteGate)
        {
            using var connection = OpenConnection();
            _ = IconThemeTokenFile(connection, iconThemeId, token);
            var rows = _iconThemeRepository.QueryAll(connection);
            var plans = new List<ResourceAssetCleanupPlan>();
            var mappings = new Dictionary<string, string>();
            foreach (var row in rows)
            {
                var mapping = ParseJsonObject(row.MappingJson);
                var tokens = JsonPath.RequiredObject(mapping, "tokens", row.Id);
                if (tokens[token] is null) continue;
                var file = IconThemeTokenFile(connection, row.Id, token).File;
                plans.Add(ResourceAssetCleanupPlan.Capture($"{row.Name} · {token}", IconThemeAssetDirectory(row.AssetRoot), file, isDirectory: false));
                tokens.Remove(token);
                mapping["categories"] = IconTokenRules.Categories(tokens);
                mappings.Add(row.Id, mapping.ToJsonString());
            }
            return AssetCleanup.Commit(connection, plans, transaction =>
            {
                foreach (var (id, mapping) in mappings)
                    _iconThemeRepository.UpdateMapping(connection, transaction, id, mapping);
            });
        }
    }

    public IconThemeTokenSvg ReadIconThemeTokenSvg(string iconThemeId, string token)
    {
        using var connection = OpenConnection();
        var (row, file) = IconThemeTokenFile(connection, iconThemeId, token);
        var path = Path.Combine(IconThemeAssetDirectory(row.AssetRoot), file);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"Missing SVG file '{file}'.");
        }

        return new IconThemeTokenSvg(token, file, File.ReadAllText(path));
    }

    public IconThemeReplaceSvgResult ReplaceIconThemeTokenSvg(string iconThemeId, string token, string svgText)
    {
        svgText = SvgReplacementService.Validate(svgText);
        lock (_context.WriteGate)
        {
            using var connection = OpenConnection();
            var (row, file) = IconThemeTokenFile(connection, iconThemeId, token);
            AssetCleanup.Write(connection, $"Replace icon · {row.Name} · {token}", _systemAssets.Root,
                new Dictionary<string, byte[]> { [ResourceAssetCleanupPlan.StoredPath(Path.Combine(row.AssetRoot, file))] = System.Text.Encoding.UTF8.GetBytes(svgText) }, _ => { });
            return new IconThemeReplaceSvgResult(token, file);
        }
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

        lock (_context.WriteGate)
        {
            using var connection = OpenConnection();
            _ = _iconThemeRepository.Get(connection, iconThemeId);
            var rows = _iconThemeRepository.QueryAll(connection);
            var prepared = rows.ToDictionary(row => row.Id, _ => new PreparedIconSource(svgText, "manual", "manual-svg-transform", true));
            var refresh = CommitIconThemeToken(connection, rows, token, IconTokenCategory(token), description, prepared);
            return new IconThemeWriteAllSvgResult(token, rows.Count, refresh);
        }
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

    private IconThemeRefreshResult RefreshIconThemeSets(SqliteConnection connection)
    {
        lock (_context.WriteGate)
        {
            var root = SystemIconThemesRoot();
            AssetCleanup.RequireAvailable(root);
            ResourceAssetCleanupPlan.RequireNoLinks(root);
            if (!Directory.Exists(root)) throw new IOException("System Icon Themes directory is unavailable; no records were changed.");
            var existing = _iconThemeRepository.QueryAll(connection).ToDictionary(row => row.Id, StringComparer.Ordinal);
            var discovered = new Dictionary<string, IconThemeRecord>(StringComparer.Ordinal);
            foreach (var directory in Directory.EnumerateDirectories(root)
                         .Where(directory => !Path.GetFileName(directory).StartsWith(".", StringComparison.Ordinal)
                             && !Path.GetFileName(directory).StartsWith("_", StringComparison.Ordinal)))
            {
                ResourceAssetCleanupPlan.RequireNoLinks(directory);
                var manifestPath = Path.Combine(directory, "manifest.json");
                ResourceAssetCleanupPlan.RequireNoLinks(manifestPath);
                var manifest = IconThemeImportDocument.Read(File.ReadAllText(manifestPath));
                var assetRoot = NormalizeRelativePath(Path.GetRelativePath(_systemAssets.Root, directory));
                var row = existing.TryGetValue(manifest.Id, out var authored)
                    ? authored with { AssetRoot = assetRoot }
                    : new IconThemeRecord(manifest.Id, manifest.Name, assetRoot, manifest.MappingJson, manifest.MetadataJson);
                if (!discovered.TryAdd(row.Id, row))
                    throw new InvalidOperationException($"Icon Theme id '{row.Id}' occurs in multiple directories.");
            }
            var rows = existing.Values.Where(row => !discovered.ContainsKey(row.Id)).Concat(discovered.Values).ToList();
            if (rows.Select(row => row.Name).Distinct(StringComparer.Ordinal).Count() != rows.Count
                || rows.Select(row => row.AssetRoot).Distinct(StringComparer.OrdinalIgnoreCase).Count() != rows.Count)
                throw new InvalidOperationException("Icon Theme identities conflict with existing names or asset locations.");
            var missing = rows.Sum(row => IconThemeTokens(row.MappingJson)
                .Count(token => !File.Exists(Path.Combine(IconThemeAssetDirectory(row.AssetRoot), token.File))));
            var tokenCount = rows.SelectMany(row => IconThemeTokens(row.MappingJson).Select(token => token.Token))
                .Distinct(StringComparer.Ordinal).Count();
            using var transaction = connection.BeginTransaction();
            foreach (var row in discovered.Values)
                _iconThemeRepository.UpsertDiscovered(connection, transaction, row.Id, row.Name, row.AssetRoot, row.MappingJson, row.MetadataJson);
            transaction.Commit();
            return new IconThemeRefreshResult(rows.Count, tokenCount, missing);
        }
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

    private static IReadOnlyList<IconThemeToken> IconThemeTokens(string mappingJson)
    {
        return IconTokenRules.Tokens(mappingJson)
            .Select((token) => new IconThemeToken(token.Token, token.Category, token.File, token.Description))
            .ToList();
    }

    internal static int IconThemeTokenCount(string mappingJson)
    {
        return IconThemeTokens(mappingJson).Count;
    }

    private static string IconTokenCategory(string token)
    {
        return IconTokenRules.CategoryFromToken(token);
    }

    private static JsonObject IconSetDefinition(IconThemeRecord row) =>
        (JsonObject)JsonPath.RequiredObject(IconThemeImportDocument.ValidateMetadata(row.MetadataJson), "iconSet", row.Id).DeepClone();

    [GeneratedRegex("^[a-z][a-z0-9_]*(?:\\.[a-z0-9_]+)*$")]
    private static partial Regex ValidIconTokenRegex();
}
