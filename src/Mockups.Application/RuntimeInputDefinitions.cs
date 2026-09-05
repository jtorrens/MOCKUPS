using Mockups.DesktopEditorShell.Common;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

public enum ComponentInputKind
{
    Text,
    Number,
    IntegerPair,
    DecimalPair,
    Boolean,
    Option,
    RecordReference,
    ComponentVariant,
    ComponentVariantSlot,
    ThemeToken,
    Icon,
    IconList,
    MultilineText,
}

public enum ComponentInputSource
{
    Runtime,
    Variant,
    Calculated,
}

public enum ComponentInputUiOrigin
{
    Self,
    Embedded,
}

public sealed record ComponentInputDefinition(
    string Id,
    string Label,
    string JsonKey,
    ComponentInputKind Kind,
    ValueKind ValueKind,
    string DefaultValue,
    IReadOnlyList<FieldOption>? Options = null,
    decimal Minimum = 0,
    decimal Maximum = 9999,
    decimal Increment = 1,
    string TableId = "",
    string ResolvedJsonKey = "",
    string ComponentType = "",
    ComponentInputSource Source = ComponentInputSource.Runtime,
    PairFieldLabels? PairLabels = null,
    ComponentInputUiOrigin UiOrigin = ComponentInputUiOrigin.Self,
    string UiGroupId = "",
    string UiGroupLabel = "",
    string UiParentGroupId = "",
    string EnabledWhenItemJsonKey = "",
    IReadOnlyList<string>? EnabledWhenItemValues = null,
    int MinimumItemIndex = 0,
    int UiOrder = 0,
    string UiSectionLabel = "",
    string Unit = "",
    AnimationFieldDefinition? Animation = null,
    BehaviorTimingDefinition? BehaviorTiming = null,
    ComponentInputTransitionDefinition? Transition = null,
    string EnabledWhenPath = "",
    string EnabledWhenValue = "",
    bool RefreshOnCommit = false,
    RuntimeInputCollectionDefinition? StructuredCollection = null,
    bool AllowEmpty = false,
    string AllowEmptyWhenItemJsonKey = "",
    IReadOnlyList<string>? AllowEmptyWhenItemValues = null,
    bool ActionOnly = false,
    string OptionsSourceCollectionJsonKey = "",
    string OptionsSourceValueJsonKey = "id",
    string OptionsSourceLabelJsonKey = "",
    string OptionsSourceFirstItemBadge = "",
    bool ShowInEditor = true,
    string HelpText = "",
    string ValuePattern = "",
    string ValuePatternMessage = "");

public sealed record RuntimeInputCollectionDefinition(
    string Id,
    string Label,
    string JsonKey,
    string ItemLabel,
    IReadOnlyList<ComponentInputDefinition> Fields,
    string SourceCollectionJsonKey = "",
    RuntimeInputCollectionItemPresentation? ItemPresentation = null,
    RuntimeComponentCollectionItemDefinition? ComponentItems = null,
    string StorageCollectionJsonKey = "",
    string ItemRuntimeContractJsonKey = "",
    string UiParentCollectionJsonKey = "",
    string UiParentItemIdJsonKey = "",
    string AnimationPresentation = "item",
    bool CanEditStructure = true,
    int FixedItemCount = 0,
    string UiPresentation = "collection",
    string ItemRuntimePresentation = "card",
    IReadOnlyList<string>? ItemRuntimeHiddenInputIds = null,
    string ItemRuntimeVariantReferencePath = "",
    string ItemRuntimeOwnerVariantReferencePath = "",
    RuntimeFixedComponentBoundaryDefinition? FixedComponentBoundary = null,
    IReadOnlySet<string>? StructureOwnedFieldJsonKeys = null);

public sealed record RuntimeFixedComponentBoundaryDefinition(
    string VariantReferenceJsonKey,
    string OverridesJsonKey,
    string ComponentType,
    string ComponentClassId);

public sealed record RuntimeComponentCollectionItemDefinition(
    string VariantReferenceJsonKey,
    string OverridesJsonKey,
    string InputsJsonKey)
{
    public RuntimeComponentCollectionDocumentKeys DocumentKeys => new(
        VariantReferenceJsonKey,
        OverridesJsonKey,
        InputsJsonKey);
}

public sealed record RuntimeInputCollectionItemPresentation(
    string TitleFieldId,
    string FirstItemBadge,
    IReadOnlyList<string> SubtitleFieldIds,
    int SubtitleMaxCharacters,
    string IconFieldId,
    string FallbackIcon,
    IReadOnlyDictionary<string, string> IconValueMap);
