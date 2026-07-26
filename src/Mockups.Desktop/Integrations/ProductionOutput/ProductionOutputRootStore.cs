using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.Integrations.ProductionOutput;

internal sealed class ProductionOutputRootStore
{
    private readonly string _path;
    private readonly object _gate = new();

    public ProductionOutputRootStore(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    public string? Get(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId)) return null;
        lock (_gate)
        {
            var document = Read();
            if (!document.ProjectRoots.TryGetValue(projectId, out var root)
                || !Path.IsPathFullyQualified(root))
            {
                return null;
            }
            return Path.GetFullPath(root);
        }
    }

    public void Set(string projectId, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(projectId))
        {
            throw new InvalidOperationException(
                "Production output root requires one exact Project.");
        }
        lock (_gate)
        {
            var document = Read();
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                document.ProjectRoots.Remove(projectId);
            }
            else
            {
                if (!Path.IsPathFullyQualified(rootPath))
                {
                    throw new InvalidOperationException(
                        "Production output root must be an absolute local directory.");
                }
                var normalized = Path.GetFullPath(rootPath);
                if (!Directory.Exists(normalized))
                {
                    throw new DirectoryNotFoundException(
                        $"Production output root does not exist: {normalized}");
                }
                document.ProjectRoots[projectId] = normalized;
            }
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
            if (document?.Schema != "mockups_production_output_roots"
                || document.Version != 1
                || document.ProjectRoots is null)
            {
                throw new InvalidOperationException(
                    "The local Production output root document is not current.");
            }
            return document;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The local Production output root document is malformed.",
                exception);
        }
    }

    private void Write(RootDocument document)
    {
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException(
                "The local Production output root document has no parent directory.");
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
        return Path.Combine(
            root,
            "MOCKUPS",
            "production-output-roots.json");
    }

    private sealed class RootDocument
    {
        public string Schema { get; init; } =
            "mockups_production_output_roots";
        public int Version { get; init; } = 1;
        public Dictionary<string, string> ProjectRoots { get; init; } =
            new(StringComparer.Ordinal);
    }
}
