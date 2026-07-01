using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal enum ValueKind
{
    StringSingleLine,
    StringReadOnly,
    StringMultiline,
    Integer,
    IntegerPair,
    DirectoryPath,
    ImageFilePath,
    HexColor,
    PaletteColorToken,
    PaletteColorPair,
    Boolean,
}

internal sealed record FieldOption(
    string Value,
    string Label,
    string? ColorHex = null);

internal sealed record FieldDefinition(
    string Id,
    string Label,
    ValueKind ValueKind,
    bool IsEditable = true,
    string DefaultValue = "",
    bool CommitAsDefault = true,
    IReadOnlyList<FieldOption>? Options = null);

internal sealed record FieldValue(
    FieldDefinition Definition,
    string Value)
{
    public bool IsDefault => Value == Definition.DefaultValue;
}
