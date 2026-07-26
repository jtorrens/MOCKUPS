using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using System;
using System.IO;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    private object WriteGate => _context.WriteGate;
    private readonly SqliteProjectContext _context;
    private readonly SqliteDesignOwner _designOwner;
    private readonly SqliteProductionOwner _productionOwner;
    private readonly SqliteResourceOwner _resourceOwner;
    private readonly IReferenceUsageService _referenceUsageService;

    public IProjectPathResolver ProjectPaths => _context.ProjectPaths;

    internal SqliteProjectContext Context => _context;

    internal SqliteResourceOwner Resources => _resourceOwner;

    internal SqliteProjectEngine(string databasePath)
        : this(new SqliteProjectContext(databasePath))
    {
    }

    internal SqliteProjectEngine(SqliteProjectContext context)
    {
        _context = context;
        _designOwner = new SqliteDesignOwner(_context);
        _productionOwner = new SqliteProductionOwner(
            _context,
            _designOwner);
        _resourceOwner = new SqliteResourceOwner(
            _context,
            _productionOwner.ProjectEpisodeRepository,
            _productionOwner.ModuleInstanceThemeContextService);
        _referenceUsageService = new ReferenceUsageService(_context);

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

        ValidateCurrentDatabase(validationConnection);
    }

    private static bool HasUserTables(SqliteConnection connection)
    {
        return ScalarLong(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'") > 0;
    }

}
