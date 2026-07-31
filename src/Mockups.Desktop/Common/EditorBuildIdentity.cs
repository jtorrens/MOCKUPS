using System;
using System.Reflection;

namespace Mockups.DesktopEditorShell.Common;

internal static class EditorBuildIdentity
{
    private const string ProductTitle =
        "Mockups Desktop Editor Shell · Design / Production";

    public static string Commit { get; } = ResolveCommit();
    public static string WindowTitle => $"{ProductTitle} · {Commit}";

    private static string ResolveCommit()
    {
        var informationalVersion = typeof(EditorBuildIdentity)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (string.IsNullOrWhiteSpace(informationalVersion)) return "unknown";
        var separator = informationalVersion.LastIndexOf('+');
        if (separator < 0 || separator == informationalVersion.Length - 1)
            return "unknown";
        var revision = informationalVersion[(separator + 1)..]
            .Split('.', 2, StringSplitOptions.RemoveEmptyEntries)[0];
        return revision.Length <= 8
            ? revision
            : revision[..8];
    }
}
