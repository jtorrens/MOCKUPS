namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record ComponentOverridePromotionBoundary(
    string SlotJsonKey,
    string VariantReferenceJsonKey,
    string OverridesJsonKey);

public abstract record ComponentOverridePromotionTarget;

public sealed record ComponentOverrideFieldPromotionTarget(
    string FieldId) : ComponentOverridePromotionTarget;

public sealed record ComponentOverrideCollectionPromotionTarget(
    string CollectionFieldId,
    StructuredCollectionAddress CollectionAddress,
    string ItemId,
    ComponentOverridePromotionBoundary Boundary)
    : ComponentOverridePromotionTarget;

public sealed record ComponentOverrideSlotPathPromotionTarget(
    IReadOnlyList<EmbeddedComponentSlotDefinition> Slots)
    : ComponentOverridePromotionTarget;

public sealed record ComponentOverridePromotionRequest(
    ProjectTreeNode OwnerNode,
    ComponentOverridePromotionTarget Target,
    string Name);
