using System;

namespace Mockups.DesktopEditorShell.EditorShell;

public static class ProductionPaletteColorNodeId
{
    private const string Separator = "::palette-value::";

    public static string Format(string projectId, string paletteColorId)
    {
        if (string.IsNullOrWhiteSpace(projectId)
            || projectId.Contains(Separator, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(paletteColorId)
            || paletteColorId.Contains(Separator, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production Palette node identity requires exact Project and System Palette ids.");
        }
        return $"{projectId}{Separator}{paletteColorId}";
    }

    public static (string ProjectId, string PaletteColorId) ParseRequired(
        string value)
    {
        var parts = value.Split(Separator, StringSplitOptions.None);
        if (parts.Length != 2
            || !Format(parts[0], parts[1]).Equals(value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Invalid Production Palette node identity '{value}'.");
        }
        return (parts[0], parts[1]);
    }
}
