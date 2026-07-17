using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static object WriteGate => SqliteProjectContext.WriteGate;
    private readonly SqliteProjectContext _context;
    private readonly IEditorLayoutRepository _editorLayoutRepository;
    private readonly IProjectEpisodeRepository _projectEpisodeRepository;
    private readonly IRenderPresetRepository _renderPresetRepository;

    public SpikeDatabase(string databasePath)
    {
        _context = new SqliteProjectContext(databasePath);
        _editorLayoutRepository = new EditorLayoutRepository(_context);
        _projectEpisodeRepository = new ProjectEpisodeRepository(_context);
        _renderPresetRepository = new RenderPresetRepository(_context);

        Initialize();
    }

    public static string DefaultDatabasePath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "package.json"))
                && Directory.Exists(Path.Combine(directory.FullName, "assets")))
            {
                return Path.Combine(directory.FullName, "data", "desktop-editor-spike.sqlite");
            }

            directory = directory.Parent;
        }

        var root = AppContext.BaseDirectory;
        return Path.GetFullPath(Path.Combine(root, "..", "..", "..", "..", "..", "data", "desktop-editor-spike.sqlite"));
    }

    private void Initialize()
    {
        if (!File.Exists(_context.DatabasePath))
        {
            throw new FileNotFoundException(
                "Desktop database does not exist. Create a validated database explicitly before opening the application.",
                _context.DatabasePath);
        }

        using var validationConnection = OpenValidationConnection();
        if (!HasUserTables(validationConnection))
        {
            throw new InvalidOperationException(
                $"Desktop database '{_context.DatabasePath}' is empty. Create a validated database explicitly before opening the application.");
        }

        ValidateSchemaV1(validationConnection);
    }

    private static bool HasUserTables(SqliteConnection connection)
    {
        return ScalarLong(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'") > 0;
    }

}
