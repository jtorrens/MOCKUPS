using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryFieldPairText
{
    public static (string First, string Second) Labels(FieldDefinition definition)
    {
        if (definition.PairLabels is not null)
        {
            return (definition.PairLabels.First, definition.PairLabels.Second);
        }

        return definition.Id switch
        {
            var id when id.EndsWith(".size", StringComparison.Ordinal) || id.EndsWith(".renderSize", StringComparison.Ordinal) => ("W", "H"),
            var id when id.EndsWith(".position", StringComparison.Ordinal) => ("X", "Y"),
            var id when id.EndsWith(".vertical", StringComparison.Ordinal) => ("Top", "Bottom"),
            var id when id.EndsWith(".horizontal", StringComparison.Ordinal) => ("Left", "Right"),
            var id when id.EndsWith(".modes", StringComparison.Ordinal) || id.StartsWith("theme.", StringComparison.Ordinal) => ("Light", "Dark"),
            _ => ("A", "B"),
        };
    }

    public static (string First, string Second) ParseRequired(
        ValueKind valueKind,
        string value,
        string owner)
    {
        var normalized = RuntimeInputValueKindContract.ParseValue(valueKind, value, owner).GetValue<string>();
        var parts = normalized.Split('|', StringSplitOptions.None);
        return (parts[0], parts[1]);
    }

    public static string Join(string first, string second)
    {
        return $"{first}|{second}";
    }
}
