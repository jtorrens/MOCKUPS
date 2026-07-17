using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private const int IconThemeScriptTimeoutMilliseconds = 30_000;

    public IconThemeSearchResult SearchIconThemeSources(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new IconThemeSearchResult([], []);
        }

        var parsed = RunIconThemeScript([
            "--mode",
            "search",
            "--query",
            query.Trim(),
        ], cancellationToken);
        var root = parsed.AsObject();
        return new IconThemeSearchResult(
            IconThemeCandidates(root, "lucide"),
            IconThemeCandidates(root, "material"));
    }

    public IconThemeGenerateResult GenerateIconThemeToken(
        string iconThemeId,
        string token,
        string category,
        string description,
        string lucideSource,
        string materialSource,
        CancellationToken cancellationToken = default)
    {
        token = token.Trim();
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
            throw new InvalidOperationException("Refresh icon sets before generating tokens.");
        }

        var requestPath = Path.Combine(Path.GetTempPath(), $"mockups-icon-generate-{Guid.NewGuid():N}.json");
        var setsRoot = Path.GetDirectoryName(Path.Combine(mediaRoot, rows[0].AssetRoot)) ?? mediaRoot;
        var request = new JsonObject
        {
            ["token"] = token,
            ["category"] = string.IsNullOrWhiteSpace(category) ? IconTokenCategory(token) : category.Trim(),
            ["description"] = description.Trim(),
            ["iconThemesRoot"] = setsRoot,
            ["mediaRoot"] = mediaRoot,
            ["selectedSources"] = new JsonObject
            {
                ["lucide"] = lucideSource,
                ["material"] = materialSource,
            },
            ["sets"] = new JsonArray(rows.Select((row) => new JsonObject
            {
                ["id"] = row.Id,
                ["name"] = Path.GetFileName(row.AssetRoot),
                ["path"] = Path.Combine(mediaRoot, row.AssetRoot),
                ["iconSet"] = IconSetDefinition(row),
            }).ToArray<JsonNode?>()),
        };
        File.WriteAllText(requestPath, request.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var parsed = RunIconThemeScript([
            "--mode",
            "generate",
            "--request",
            requestPath,
        ], cancellationToken);
        var refresh = RefreshIconThemeSets(connection, projectId);
        UpdateIconThemeTokenMetadata(connection, projectId, token, category, description, lucideSource, materialSource);
        return new IconThemeGenerateResult(token, JsonInt(parsed, ["writtenFileCount"], rows.Count), refresh);
    }

    private static IReadOnlyList<IconThemeSearchCandidate> IconThemeCandidates(JsonObject root, string provider)
    {
        if (root[provider] is not JsonArray array) return [];
        return array
            .OfType<JsonObject>()
            .Select((entry) => new IconThemeSearchCandidate(
                provider,
                JsonString(entry, ["sourceName"]),
                JsonString(entry, ["previewUrl"])))
            .Where((entry) => !string.IsNullOrWhiteSpace(entry.SourceName))
            .ToList();
    }

    private static JsonNode RunIconThemeScript(string[] arguments, CancellationToken cancellationToken)
    {
        var scriptCandidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "scripts", "icon-themes", "sync-icon-theme-token.cjs"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "scripts", "icon-themes", "sync-icon-theme-token.cjs"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "scripts", "icon-themes", "sync-icon-theme-token.cjs"),
        }
            .Select(Path.GetFullPath)
            .ToList();
        var scriptPath = scriptCandidates.FirstOrDefault(File.Exists)
            ?? throw new InvalidOperationException($"Icon theme script not found. Checked: {string.Join(", ", scriptCandidates)}");
        var workingDirectory = Directory.GetParent(scriptPath)?.Parent?.Parent?.FullName ?? AppContext.BaseDirectory;
        var startInfo = DesktopChildProcess.CreateHiddenStartInfo("node", workingDirectory);
        startInfo.ArgumentList.Add(scriptPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = System.Diagnostics.Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start icon theme script.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        var stopwatch = Stopwatch.StartNew();
        while (!process.WaitForExit(100))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                KillProcessTree(process);
                throw new OperationCanceledException(cancellationToken);
            }

            if (stopwatch.ElapsedMilliseconds >= IconThemeScriptTimeoutMilliseconds)
            {
                KillProcessTree(process);
                throw new InvalidOperationException("Icon theme script timed out after 30 seconds.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "Icon theme script failed." : stderr.Trim());
        }

        return JsonNode.Parse(stdout) ?? new JsonObject();
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            process.WaitForExit(2_000);
        }
        catch
        {
            // A stuck provider process should not keep the editor blocked.
        }
    }

    private static int JsonInt(JsonNode node, IReadOnlyList<string> path, int fallback)
    {
        if (node is not JsonObject root) return fallback;
        var value = GetJsonValue(root, path);
        return value is JsonValue jsonValue && jsonValue.TryGetValue<int>(out var parsed) ? parsed : fallback;
    }

    private void UpdateIconThemeTokenMetadata(
        SqliteConnection connection,
        string projectId,
        string token,
        string category,
        string description,
        string lucideSource,
        string materialSource)
    {
        var rows = _iconThemeRepository.QueryAll(connection).Where((row) => row.ProjectId == projectId).ToList();
        foreach (var row in rows)
        {
            var mapping = ParseJsonObject(row.MappingJson);
            var tokens = mapping["tokens"] as JsonObject ?? [];
            var tokenObject = tokens[token] as JsonObject ?? [];
            tokenObject["category"] = string.IsNullOrWhiteSpace(category) ? IconTokenCategory(token) : category.Trim();
            tokenObject["description"] = description.Trim();
            tokenObject["file"] = $"{token}.svg";
            tokenObject["sources"] = new JsonObject
            {
                ["lucide"] = lucideSource,
                ["material"] = materialSource,
            };
            tokens[token] = tokenObject;
            mapping["tokens"] = tokens;
            _iconThemeRepository.UpdateMapping(connection, row.Id, mapping.ToJsonString());
        }
    }
}
