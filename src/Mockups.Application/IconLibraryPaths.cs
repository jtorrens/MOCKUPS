using System;
using System.IO;

namespace Mockups.DesktopEditorShell.Common;

/// <summary>The editable library belongs to the database workspace, never the executable.</summary>
public static class IconLibraryPaths
{
    public const string DirectoryName = "icon-library";

    public static string ForDatabase(string databasePath)
    {
        if (!Path.IsPathFullyQualified(databasePath))
            throw new ArgumentException("The icon library requires an absolute database path.", nameof(databasePath));
        return Path.Combine(Path.GetDirectoryName(databasePath)!, DirectoryName);
    }
}
