using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.Integrations.ProductionOutput;

internal sealed record ShotManagerLocalProduction(
    string DocumentPath,
    string RootPath,
    ShotManagerReadonlyProduction Production);

internal sealed record ShotManagerProjectLocation(
    string DocumentPath,
    string RootPath,
    bool RequestedEnabled,
    string PendingWorkstreamName);

internal sealed class ShotManagerDocumentStore
{
    private readonly string _path;
    private readonly object _gate = new();

    public ShotManagerDocumentStore(string? path = null)
    {
        _path = path ?? DefaultPath();
    }

    public ShotManagerProjectLocation GetLocation(string projectId)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new InvalidOperationException(
                "Shot Manager location requires one exact Project.");
        lock (_gate)
        {
            var document = Read();
            return document.ProjectLocations.TryGetValue(
                    projectId,
                    out var location)
                ? RequireLocation(location, projectId)
                : new ShotManagerProjectLocation("", "", false, "");
        }
    }

    public string? Get(string projectId)
    {
        var value = GetLocation(projectId).DocumentPath;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public string? GetRoot(string projectId)
    {
        var value = GetLocation(projectId).RootPath;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public ShotManagerLocalProduction ValidateDocument(string documentPath) =>
        OpenPath(documentPath);

    public void SetValidated(
        string projectId,
        ShotManagerLocalProduction production) =>
        Update(projectId, (current) => current with
        {
            DocumentPath = production.DocumentPath,
            RootPath = production.RootPath,
        });

    public void SetRequestedEnabled(string projectId, bool enabled) =>
        Update(projectId, (current) => current with
        {
            RequestedEnabled = enabled,
        });

    public void SetPendingWorkstream(
        string projectId,
        string workstreamName) =>
        Update(projectId, (current) => current with
        {
            PendingWorkstreamName = workstreamName.Trim(),
        });

    public void SetRoot(string projectId, string rootPath)
    {
        var canonical = RequireRealRoot(rootPath);
        Update(projectId, (current) => current with { RootPath = canonical });
    }

    public ShotManagerLocalProduction Open(string projectId)
    {
        var path = GetLocation(projectId).DocumentPath;
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(
                "Choose this workstation's Shot Manager production.json before creating or refreshing associations.");
        return OpenPath(path);
    }

    private void Update(
        string projectId,
        Func<ShotManagerProjectLocation, ShotManagerProjectLocation> update)
    {
        if (string.IsNullOrWhiteSpace(projectId))
            throw new InvalidOperationException(
                "Shot Manager location requires one exact Project.");
        lock (_gate)
        {
            var document = Read();
            var current = document.ProjectLocations.TryGetValue(
                    projectId,
                    out var existing)
                ? RequireLocation(existing, projectId)
                : new ShotManagerProjectLocation("", "", false, "");
            document.ProjectLocations[projectId] = RequireLocation(
                update(current),
                projectId);
            Write(document);
        }
    }

    private static ShotManagerLocalProduction OpenPath(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidOperationException(
                "Shot Manager production.json must use an absolute local path.");
        var info = new FileInfo(Path.GetFullPath(path));
        if (!info.Exists)
            throw new FileNotFoundException(
                $"Shot Manager production.json does not exist: {info.FullName}");
        var target = info.ResolveLinkTarget(returnFinalTarget: true);
        var canonical = target?.FullName ?? info.FullName;
        if (!Path.GetFileName(canonical).Equals(
                "production.json",
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Choose a file named exactly production.json.");
        var canonicalInfo = new FileInfo(canonical);
        if (!canonicalInfo.Exists
            || canonicalInfo.Attributes.HasFlag(FileAttributes.Directory))
            throw new InvalidOperationException(
                "Shot Manager production.json must resolve to a regular file.");
        var root = canonicalInfo.DirectoryName
            ?? throw new InvalidOperationException(
                "Shot Manager production.json has no containing directory.");
        var production = ShotManagerReadonlyContract.ParseRequired(
            File.ReadAllText(canonicalInfo.FullName),
            $"Shot Manager document '{canonicalInfo.FullName}'");
        return new ShotManagerLocalProduction(
            canonicalInfo.FullName,
            RequireRealRoot(root),
            production);
    }

    private static string RequireRealRoot(string rootPath)
    {
        if (!Path.IsPathFullyQualified(rootPath))
            throw new InvalidOperationException(
                "Shot Manager Production root must be absolute.");
        var info = new DirectoryInfo(Path.GetFullPath(rootPath));
        if (!info.Exists)
            throw new DirectoryNotFoundException(
                $"Shot Manager Production root does not exist: {info.FullName}");
        if (info.LinkTarget is not null
            || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            throw new IOException(
                $"Shot Manager Production root cannot be a symbolic link: {info.FullName}");
        return info.FullName;
    }

    private static ShotManagerProjectLocation RequireLocation(
        ShotManagerProjectLocation location,
        string projectId)
    {
        if ((!string.IsNullOrEmpty(location.DocumentPath)
                && !Path.IsPathFullyQualified(location.DocumentPath))
            || (!string.IsNullOrEmpty(location.RootPath)
                && !Path.IsPathFullyQualified(location.RootPath))
            || !location.PendingWorkstreamName.Equals(
                location.PendingWorkstreamName.Trim(),
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Project '{projectId}' has an invalid local Shot Manager location.");
        return location;
    }

    private StoreDocument Read()
    {
        if (!File.Exists(_path)) return new StoreDocument();
        try
        {
            var document = JsonSerializer.Deserialize<StoreDocument>(
                File.ReadAllText(_path));
            if (document?.Schema != "mockups_shot_manager_locations"
                || document.Version != 1
                || document.ProjectLocations is null)
                throw new InvalidOperationException(
                    "The local Shot Manager location document is not current.");
            return document;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "The local Shot Manager location document is malformed.",
                exception);
        }
    }

    private void Write(StoreDocument document)
    {
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException(
                "The local Shot Manager location document has no parent directory.");
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
        if (string.IsNullOrWhiteSpace(root)) root = Path.GetTempPath();
        return Path.Combine(root, "MOCKUPS", "shot-manager-locations.json");
    }

    private sealed class StoreDocument
    {
        public string Schema { get; init; } =
            "mockups_shot_manager_locations";
        public int Version { get; init; } = 1;
        public Dictionary<string, ShotManagerProjectLocation> ProjectLocations
            { get; init; } = new(StringComparer.Ordinal);
    }
}
