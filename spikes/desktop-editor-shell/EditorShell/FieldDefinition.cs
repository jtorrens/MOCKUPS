using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal enum ValueKind
{
    StringSingleLine,
    StringReadOnly,
    StringMultiline,
    Integer,
    Decimal,
    HueDegrees,
    IntegerPair,
    DirectoryPath,
    ImageFilePath,
    OptionToken,
    RecordReference,
    ThemeToken,
    ThemeTokenPair,
    TypographyStyle,
    HexColor,
    Alpha,
    PaletteColorToken,
    PaletteColorPair,
    PaletteColorAlphaPair,
    IconToken,
    IconTokenList,
    IconSlots,
    EmbeddedComponent,
    ComponentPreset,
    ComponentInputBindings,
    AlignmentPlacement,
    Motion,
    MotionTiming,
    Boolean,
}

internal sealed record FieldOption(
    string Value,
    string Label,
    string? ColorHex = null,
    bool IsNeutral = false)
{
    public override string ToString()
    {
        return Label;
    }
}

internal sealed record PairFieldLabels(string First, string Second);

internal sealed record NumberDefinition(
    decimal? Minimum = null,
    decimal? Maximum = null,
    decimal Increment = 1,
    int DecimalPlaces = 0,
    bool UseSlider = false);

internal sealed record RecordReferenceDefinition(string TableId);

internal enum ComponentInputBindingSource
{
    Variant,
    Runtime,
    Calculated,
}

internal sealed record ComponentInputBindingDefinition(
    string Id,
    string Label,
    string JsonKey,
    ValueKind ValueKind,
    ComponentInputBindingSource Source,
    string DefaultValue = "",
    IReadOnlyList<FieldOption>? Options = null,
    NumberDefinition? Number = null,
    string ComponentType = "",
    string UiGroupId = "",
    string UiGroupLabel = "");

internal enum ImagePreviewMode
{
    Aspect,
    SquareCrop,
}

internal sealed record ImagePreviewDefinition(
    ImagePreviewMode Mode,
    int BaseSize = 0,
    string? ScaleFieldId = null,
    string? OffsetFieldId = null);

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
    IReadOnlyList<FieldOption>? Options = null,
    PairFieldLabels? PairLabels = null,
    ImagePreviewDefinition? ImagePreview = null,
    NumberDefinition? Number = null,
    RecordReferenceDefinition? RecordReference = null,
    IReadOnlyList<ComponentInputBindingDefinition>? ComponentInputBindings = null);

internal sealed record FieldValue(
    FieldDefinition Definition,
    string Value,
    bool IsInherited = false,
    bool IsHighlighted = false)
{
    public bool HasLocalOverride => Definition.CanInherit && !IsInherited;
    public bool IsDefault => Definition.CanInherit ? IsInherited : Value == Definition.DefaultValue;
}
