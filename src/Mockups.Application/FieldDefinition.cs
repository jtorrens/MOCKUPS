using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

public enum ValueKind
{
    StringSingleLine,
    StringReadOnly,
    StringMultiline,
    Integer,
    Decimal,
    HueDegrees,
    IntegerPair,
    DirectoryPath,
    JsonFilePath,
    ImageFilePath,
    MediaFilePath,
    VideoFilePath,
    OptionToken,
    RecordReference,
    ThemeToken,
    ThemeTokenPair,
    TypographyStyle,
    TypographySystemStyle,
    HexColor,
    Alpha,
    PaletteColorToken,
    PaletteColorPair,
    PaletteColorAlphaPair,
    IconToken,
    IconTokenList,
    EmbeddedComponent,
    ComponentVariant,
    ComponentVariantSlot,
    ComponentInputBindings,
    StructuredCollection,
    AlignmentPlacement,
    Motion,
    MotionTiming,
    BehaviorTiming,
    Boolean,
}

public sealed record FieldOption(
    string Value,
    string Label,
    string? ColorHex = null,
    bool IsNeutral = false,
    string GroupValue = "",
    string GroupLabel = "",
    string LocalLabel = "")
{
    public override string ToString()
    {
        return Label;
    }
}

public sealed record PairFieldLabels(string First, string Second);

public sealed record NumberDefinition(
    decimal? Minimum = null,
    decimal? Maximum = null,
    decimal Increment = 1,
    int DecimalPlaces = 0,
    bool UseSlider = false);

public sealed record RecordReferenceDefinition(
    string TableId,
    string Filter = "",
    bool AllowEmpty = false,
    string OverrideRecordClassId = "",
    string OverrideDocumentFieldId = "",
    IReadOnlyList<string>? OverrideFieldIds = null);

public sealed record AnimationFieldDefinition(
    IReadOnlyList<string> Interpolations,
    bool ExtendsOwnerDuration = true,
    string BaseDurationFieldId = "",
    int MinimumEnabledKeyframes = 2);

public sealed record BehaviorTimingDefinition(
    string SourceFieldId,
    string Unit,
    double BaseFramesPerUnit);

public sealed record MotionTimingDefinition(
    bool ShowDuration = true,
    bool ShowDelay = true,
    bool ShowEasing = true,
    bool ShowIntensity = true);

public enum ComponentInputBindingSource
{
    Variant,
    Runtime,
    Calculated,
}

public sealed record ComponentInputTransitionDefinition(
    string TargetInputId,
    IReadOnlyList<string> TriggerValues,
    string ReplacementValue,
    string TargetValuePattern = "",
    bool ForwardedTargetOnly = false);

public sealed record ComponentInputBindingDefinition(
    string Id,
    string Label,
    string JsonKey,
    ValueKind ValueKind,
    ComponentInputBindingSource Source,
    string DefaultValue = "",
    IReadOnlyList<FieldOption>? Options = null,
    NumberDefinition? Number = null,
    PairFieldLabels? PairLabels = null,
    string ComponentType = "",
    string UiGroupId = "",
    string UiGroupLabel = "",
    string TableId = "",
    string ResolvedJsonKey = "",
    string UiParentGroupId = "",
    int UiOrder = 0,
    string UiSectionLabel = "",
    ComponentInputTransitionDefinition? Transition = null,
    AnimationFieldDefinition? Animation = null,
    BehaviorTimingDefinition? BehaviorTiming = null,
    bool ActionOnly = false,
    RuntimeInputCollectionDefinition? StructuredCollection = null);

public enum ImagePreviewMode
{
    Aspect,
    SquareCrop,
}

public sealed record ImagePreviewDefinition(
    ImagePreviewMode Mode,
    int BaseSize = 0,
    string? ScaleFieldId = null,
    string? OffsetFieldId = null);

public sealed record FieldDefinition(
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
    IReadOnlyList<ComponentInputBindingDefinition>? ComponentInputBindings = null,
    RuntimeInputCollectionDefinition? StructuredCollection = null,
    string RuntimeInputComponentVariantFieldId = "",
    string RuntimeCollectionComponentVariantFieldId = "",
    bool SelectComponentClass = false,
    string Unit = "",
    AnimationFieldDefinition? Animation = null,
    BehaviorTimingDefinition? BehaviorTiming = null,
    MotionTimingDefinition? MotionTiming = null,
    string HelpText = "",
    string ValuePattern = "",
    string ValuePatternMessage = "")
{
    public string DisplayLabel => string.IsNullOrWhiteSpace(Unit)
        ? Label
        : $"{Label} ({Unit})";
}

public sealed record FieldValue(
    FieldDefinition Definition,
    string Value,
    bool IsInherited = false,
    bool IsHighlighted = false)
{
    public bool HasLocalOverride => Definition.CanInherit && !IsInherited;
    public bool IsDefault => Definition.CanInherit ? IsInherited : Value == Definition.DefaultValue;
}
