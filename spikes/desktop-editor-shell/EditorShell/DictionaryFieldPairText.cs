using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class DictionaryFieldPairText
{
    public static (string First, string Second) Labels(FieldDefinition definition)
    {
        var labels = PairFieldLabelsContract.Require(
            definition.PairLabels,
            $"Dictionary field '{definition.Id}'");
        return (labels.First, labels.Second);
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
