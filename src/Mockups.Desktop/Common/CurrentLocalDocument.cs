using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mockups.DesktopEditorShell.Common;

internal static class CurrentLocalDocument
{
    public static readonly JsonSerializerOptions ExactJson = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static string ApplicationDataPath(string fileName)
    {
        var root = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            throw new InvalidOperationException(
                "The operating system did not provide a local application data directory.");
        }
        return Path.Combine(root, "MOCKUPS", fileName);
    }
}
