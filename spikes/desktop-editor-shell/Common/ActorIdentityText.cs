using System;
using System.Linq;

namespace Mockups.DesktopEditorShell.Common;

internal static class ActorIdentityText
{
    public static string Initials(string shortName, string displayName)
    {
        var source = string.IsNullOrWhiteSpace(shortName) ? displayName : shortName;
        var parts = source.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Concat(parts.Take(2).Select((part) => part[0])).ToUpperInvariant();
    }
}
