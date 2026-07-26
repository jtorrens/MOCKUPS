using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.Integrations.ShotManager;

internal sealed class ShotManagerWorkstationRootStore
{
    private readonly string _path;
    private readonly object _gate = new();

    public ShotManagerWorkstationRootStore(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    public string? Get(string productionId)
    {
        if (string.IsNullOrWhiteSpace(productionId)) return null;
        lock (_gate)
        {
            var document = Read();
            if (!document.ProductionRoots.TryGetValue(productionId, out var root)
                || !Path.IsPathFullyQualified(root))
            {
                return null;
            }
            return Path.GetFullPath(root);
        }
    }

    public void Remember(string productionId, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(productionId)
            || !Path.IsPathFullyQualified(rootPath))
        {
            throw new InvalidOperationException(
                "Shot Manager workstation roots require an exact Production and absolute local path.");
        }
        lock (_gate)
        {
            var document = Read();
            document.ProductionRoots[productionId] = Path.GetFullPath(rootPath);
            Write(document);
        }
    }

    private RootDocument Read()
    {
        if (!File.Exists(_path)) return new RootDocument();
        try
        {
            var document = JsonSerializer.Deserialize<RootDocument>(
                File.ReadAllText(_path));
            if (document?.Schema != "mockups_shot_manager_workstation_roots"
                || document.Version != 1
                || document.ProductionRoots is null)
            {
                throw new InvalidOperationException(
                    "The local Shot Manager root cache is not a current document.");
            }
            return document;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The local Shot Manager root cache is malformed.",
                exception);
        }
    }

    private void Write(RootDocument document)
    {
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException(
                "The local Shot Manager root cache has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporary = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(
                    document,
                    new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static string DefaultPath()
    {
        var root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.GetTempPath();
        }
        return Path.Combine(root, "MOCKUPS", "shot-manager-roots.json");
    }

    private sealed class RootDocument
    {
        public string Schema { get; init; } =
            "mockups_shot_manager_workstation_roots";
        public int Version { get; init; } = 1;
        public Dictionary<string, string> ProductionRoots { get; init; } =
            new(StringComparer.Ordinal);
    }
}
