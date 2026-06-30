namespace Mockups.DesktopEditorShell.EditorShell;

internal enum ValueKind
{
    StringSingleLine,
    StringReadOnly,
    StringMultiline,
}

internal sealed record FieldDefinition(
    string Id,
    string Label,
    ValueKind ValueKind,
    bool IsEditable = true,
    string DefaultValue = "");

internal sealed record FieldValue(
    FieldDefinition Definition,
    string Value)
{
    public bool IsDefault => Value == Definition.DefaultValue;
}
