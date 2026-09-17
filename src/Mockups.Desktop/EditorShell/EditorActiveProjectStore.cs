using Mockups.DesktopEditorShell.Common;
using System;
using System.IO;
using System.Text.Json;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorActiveProjectStore
{
    private const string CurrentSchema = "mockups_active_project";
    private const int CurrentVersion = 1;
    private readonly string _path;

    public EditorActiveProjectStore(string? shellStatePath = null)
    {
        _path = string.IsNullOrWhiteSpace(shellStatePath)
            ? CurrentLocalDocument.ApplicationDataPath(
                "active-project.json")
            : Path.Combine(
                Path.GetDirectoryName(
                    Path.GetFullPath(shellStatePath))!,
                "active-project.json");
    }

    public string Restore()
    {
        if (!File.Exists(_path))
        {
            return "";
        }

        try
        {
            var document = JsonSerializer.Deserialize<Document>(
                    File.ReadAllText(_path),
                    CurrentLocalDocument.ExactJson)
                ?? throw new InvalidDataException(
                    "The active Project document is empty.");
            if (!document.Schema.Equals(
                    CurrentSchema,
                    StringComparison.Ordinal)
                || document.Version != CurrentVersion
                || string.IsNullOrWhiteSpace(
                    document.ActiveProjectId))
            {
                throw new InvalidDataException(
                    "The active Project document does not satisfy its current contract.");
            }
            return document.ActiveProjectId;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "The active Project document is malformed or incomplete.",
                exception);
        }
    }

    public void Save(string activeProjectId)
    {
        if (string.IsNullOrWhiteSpace(activeProjectId))
        {
            return;
        }

        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException(
                "The active Project document has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporary = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(
                    new Document
                    {
                        Schema = CurrentSchema,
                        Version = CurrentVersion,
                        ActiveProjectId = activeProjectId,
                    },
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                    }));
            File.Move(
                temporary,
                _path,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private sealed class Document
    {
        public required string Schema { get; init; }
        public required int Version { get; init; }
        public required string ActiveProjectId { get; init; }
    }
}
