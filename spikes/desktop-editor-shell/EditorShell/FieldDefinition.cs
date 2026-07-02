using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal enum ValueKind
{
    StringSingleLine,
    StringReadOnly,
    StringMultiline,
    Integer,
    HueDegrees,
    IntegerPair,
    DirectoryPath,
    ImageFilePath,
    OptionToken,
    ThemeToken,
    HexColor,
    PaletteColorToken,
    PaletteColorPair,
    IconSlots,
    Boolean,
}

internal sealed record FieldOption(
    string Value,
    string Label,
    string? ColorHex = null)
{
    public override string ToString()
    {
        return Label;
    }
}

internal sealed record FieldDefinition(
    string Id,
    string Label,
    ValueKind ValueKind,
    bool IsEditable = true,
    string DefaultValue = "",
    bool CommitAsDefault = true,
    bool CanInherit = false,
    string InheritedValue = "",
    string InheritedStorageValue = "inherited",
    IReadOnlyList<FieldOption>? Options = null);

internal sealed record FieldValue(
    FieldDefinition Definition,
    string Value,
    bool IsInherited = false)
{
    public bool HasLocalOverride => Definition.CanInherit && !IsInherited;
    public bool IsDefault => Definition.CanInherit ? IsInherited : Value == Definition.DefaultValue;
}
