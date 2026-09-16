namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record ComponentOverridePromotionBoundary(
    string SlotJsonKey,
    string VariantReferenceJsonKey,
    string OverridesJsonKey);

public sealed record ComponentOverridePromotionRequest(
    ProjectTreeNode OwnerNode,
    string CollectionFieldId,
    StructuredCollectionAddress CollectionAddress,
    string ItemId,
    ComponentOverridePromotionBoundary Boundary,
    string Name);

public sealed record ComponentOverrideFieldPromotionRequest(
    ProjectTreeNode OwnerNode,
    string FieldId,
    string Name);
