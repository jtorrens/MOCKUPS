using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    public ProjectTreeNode PromoteOverridesToVariant(
        ComponentOverridePromotionRequest request) =>
        request.Target switch
        {
            ComponentOverrideFieldPromotionTarget target =>
                PromoteFieldOverridesToVariant(
                    new FieldPromotionRequest(
                        request.OwnerNode,
                        target.FieldId,
                        request.Name)),
            ComponentOverrideCollectionPromotionTarget target =>
                PromoteCollectionOverridesToVariant(
                    new CollectionPromotionRequest(
                        request.OwnerNode,
                        target.CollectionFieldId,
                        target.CollectionAddress,
                        target.ItemId,
                        target.Boundary,
                        request.Name)),
            ComponentOverrideSlotPathPromotionTarget target =>
                PromoteEmbeddedOverridesToVariant(
                    new SlotPathPromotionRequest(
                        request.OwnerNode,
                        target.Slots,
                        request.Name)),
            _ => throw new InvalidOperationException(
                "Unknown Component Override promotion target."),
        };

    private ProjectTreeNode PromoteFieldOverridesToVariant(
        FieldPromotionRequest request) =>
        request.OwnerNode.Kind switch
        {
            ProjectTreeNodeKind.ModuleVariant =>
                PromoteModuleFieldOverridesToVariant(request),
            ProjectTreeNodeKind.ComponentVariant =>
                PromoteComponentFieldOverridesToVariant(request),
            _ => throw new InvalidOperationException(
                "Component Override promotion requires an editable Variant owner."),
        };

    private ProjectTreeNode PromoteModuleFieldOverridesToVariant(
        FieldPromotionRequest request)
    {
        if (request.OwnerNode.Kind != ProjectTreeNodeKind.ModuleVariant
            || !VariantReferenceId.TryParse(
                request.OwnerNode.Id,
                out var moduleId,
                out var moduleVariantId))
        {
            throw new InvalidOperationException(
                "Component Override promotion requires a Module Variant owner.");
        }
        if (request.OwnerNode.IsLocked)
        {
            throw new InvalidOperationException(
                $"Module Variant '{request.OwnerNode.Name}' is locked.");
        }
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                "Variant name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var module = _appModuleRepository.GetModule(
                connection,
                moduleId);
            var moduleMetadata = ParseJsonObject(
                module.MetadataJson);
            var moduleVariant = VariantEnvelopeContract.FindSource(
                    VariantEnvelopeContract.RequiredArray(
                        moduleMetadata,
                        "variants",
                        $"Module '{moduleId}'"),
                    moduleVariantId)
                ?? throw new InvalidOperationException(
                    $"Missing Module Variant '{request.OwnerNode.Id}'.");
            if (JsonBool(moduleVariant, ["locked"]))
            {
                throw new InvalidOperationException(
                    $"Module Variant '{request.OwnerNode.Name}' is locked.");
            }
            var moduleConfig = moduleVariant["config"] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Module Variant '{request.OwnerNode.Id}' has no config.");
            var field = RecordClassFieldCatalog.Get(
                request.FieldId);
            if (field.ValueKind != ValueKind.ComponentVariantSlot
                || field.ConfigJsonPath is not { Length: > 0 } path)
            {
                throw new InvalidOperationException(
                    $"Module field '{request.FieldId}' is not a Component Variant Slot.");
            }
            var slot = JsonPath.Get(moduleConfig, path) as JsonObject
                ?? throw new InvalidOperationException(
                    $"Module field '{request.FieldId}' must be a Component Variant Slot object.");
            var owner = $"Module field '{request.FieldId}'";
            var overrides = ComponentVariantSlotDocumentContract.Overrides(
                slot,
                owner);
            if (!OverrideDocumentContract.HasAuthoredValues(overrides))
            {
                throw new InvalidOperationException(
                    "Only authored Overrides can be converted into a Variant.");
            }
            var promoted = PreparePromotedVariant(
                connection,
                ComponentVariantSlotDocumentContract.VariantReference(
                    slot,
                    owner),
                overrides,
                name);
            JsonPath.Set(
                moduleConfig,
                path,
                ComponentVariantSlotDocumentContract.Create(
                    promoted.Reference,
                    new JsonObject(),
                    owner));
            CurrentModuleConfigContract.Validate(
                module.RecordClassId,
                moduleConfig,
                $"Module Variant '{request.OwnerNode.Id}' config");
            moduleVariant["config"] = moduleConfig;
            var moduleMetadataJson = moduleMetadata.ToJsonString();
            global::Mockups.DesktopEditorShell.Data.AppModuleRepository.ValidateModuleMetadata(
                moduleMetadataJson,
                moduleId,
                module.RecordClassId);

            _commitVariants(connection,
            [
                new(ProjectTreeNodeKind.ComponentVariant, promoted.Component.Id, promoted.MetadataJson),
                new(ProjectTreeNodeKind.ModuleVariant, moduleId, moduleMetadataJson),
            ]);
            return PromotedVariantNode(
                promoted,
                name);
        }
    }

    private ProjectTreeNode PromoteComponentFieldOverridesToVariant(
        FieldPromotionRequest request)
    {
        var (ownerComponentId, ownerVariantId, name) =
            RequiredComponentVariantOwner(
                request.OwnerNode,
                request.Name);

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var ownerComponent = _componentClassRepository.Get(
                connection,
                ownerComponentId);
            var ownerMetadata = ParseJsonObject(
                ownerComponent.MetadataJson);
            var ownerVariant = RequiredEditableComponentVariant(
                ownerMetadata,
                ownerComponentId,
                ownerVariantId);
            var ownerConfig = ownerVariant["config"] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Component Variant '{request.OwnerNode.Id}' has no config.");
            var field = ComponentClassFieldCatalog.Get(
                request.FieldId);
            if (field.ValueKind != ValueKind.ComponentVariantSlot
                || field.JsonPath is not { Length: > 0 } path)
            {
                throw new InvalidOperationException(
                    $"Component field '{request.FieldId}' is not a Component Variant Slot.");
            }
            var slot = JsonPath.Get(ownerConfig, path) as JsonObject
                ?? throw new InvalidOperationException(
                    $"Component field '{request.FieldId}' must be a Component Variant Slot object.");
            var owner = $"Component field '{request.FieldId}'";
            var overrides = ComponentVariantSlotDocumentContract.Overrides(
                slot,
                owner);
            if (!OverrideDocumentContract.HasAuthoredValues(overrides))
            {
                throw new InvalidOperationException(
                    "Only authored Overrides can be converted into a Variant.");
            }
            var promoted = PreparePromotedVariant(
                connection,
                ComponentVariantSlotDocumentContract.VariantReference(
                    slot,
                    owner),
                overrides,
                name);

            var finalOwnerMetadata = promoted.Component.Id.Equals(
                    ownerComponentId,
                    StringComparison.Ordinal)
                ? ParseJsonObject(promoted.MetadataJson)
                : ownerMetadata;
            var finalOwnerVariant = RequiredEditableComponentVariant(
                finalOwnerMetadata,
                ownerComponentId,
                ownerVariantId);
            var finalOwnerConfig = finalOwnerVariant["config"] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Component Variant '{request.OwnerNode.Id}' has no config.");
            JsonPath.Set(
                finalOwnerConfig,
                path,
                ComponentVariantSlotDocumentContract.Create(
                    promoted.Reference,
                    new JsonObject(),
                    owner));
            var ownerMetadataJson = finalOwnerMetadata.ToJsonString();

            var changes = new List<VariantDocumentChange>();
            if (promoted.Component.Id != ownerComponentId)
                changes.Add(new(ProjectTreeNodeKind.ComponentVariant, promoted.Component.Id, promoted.MetadataJson));
            changes.Add(new(ProjectTreeNodeKind.ComponentVariant, ownerComponentId, ownerMetadataJson));
            _commitVariants(connection, changes);
            return PromotedVariantNode(
                promoted,
                name);
        }
    }

    private ProjectTreeNode PromoteCollectionOverridesToVariant(
        CollectionPromotionRequest request) =>
        request.OwnerNode.Kind switch
        {
            ProjectTreeNodeKind.ModuleVariant =>
                PromoteModuleCollectionOverridesToVariant(request),
            ProjectTreeNodeKind.ComponentVariant =>
                PromoteComponentCollectionOverridesToVariant(request),
            _ => throw new InvalidOperationException(
                "Component Override promotion requires an editable Variant owner."),
        };

    private ProjectTreeNode PromoteModuleCollectionOverridesToVariant(
        CollectionPromotionRequest request)
    {
        if (request.OwnerNode.Kind != ProjectTreeNodeKind.ModuleVariant
            || !VariantReferenceId.TryParse(
                request.OwnerNode.Id,
                out var moduleId,
                out var moduleVariantId))
        {
            throw new InvalidOperationException(
                "Component Override promotion requires a Module Variant owner.");
        }
        if (request.OwnerNode.IsLocked)
        {
            throw new InvalidOperationException(
                $"Module Variant '{request.OwnerNode.Name}' is locked.");
        }
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                "Variant name cannot be empty.");
        }

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var module = _appModuleRepository.GetModule(
                connection,
                moduleId);
            var moduleMetadata = ParseJsonObject(
                module.MetadataJson);
            var moduleVariant = VariantEnvelopeContract.FindSource(
                    VariantEnvelopeContract.RequiredArray(
                        moduleMetadata,
                        "variants",
                        $"Module '{moduleId}'"),
                    moduleVariantId)
                ?? throw new InvalidOperationException(
                    $"Missing Module Variant '{request.OwnerNode.Id}'.");
            if (JsonBool(moduleVariant, ["locked"]))
            {
                throw new InvalidOperationException(
                    $"Module Variant '{request.OwnerNode.Name}' is locked.");
            }
            var moduleConfig = moduleVariant["config"] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Module Variant '{request.OwnerNode.Id}' has no config.");
            var field = RecordClassFieldCatalog.Get(
                request.CollectionFieldId);
            var definition = field.StructuredCollection
                ?? throw new InvalidOperationException(
                    $"Module field '{request.CollectionFieldId}' is not a structured collection.");
            var items = JsonPath.Get(
                    moduleConfig,
                    field.ConfigJsonPath
                        ?? throw new InvalidOperationException(
                            $"Module field '{request.CollectionFieldId}' has no config path."))
                as JsonArray
                ?? throw new InvalidOperationException(
                    $"Module field '{request.CollectionFieldId}' must be an array.");
            var item = RequiredCollectionItem(
                items,
                definition,
                request.CollectionAddress,
                request.ItemId);
            var (variantReference, overrides) = ReadBoundary(
                item,
                request.Boundary,
                request.CollectionFieldId);
            if (!OverrideDocumentContract.HasAuthoredValues(overrides))
            {
                throw new InvalidOperationException(
                    "Only authored Overrides can be converted into a Variant.");
            }
            var promoted = PreparePromotedVariant(
                connection,
                variantReference,
                overrides,
                name);
            WriteBoundary(
                item,
                request.Boundary,
                promoted.Reference,
                request.CollectionFieldId);

            StructuredCollectionDocumentContract.Validate(
                items,
                definition,
                $"Module field '{request.CollectionFieldId}'");
            CurrentModuleConfigContract.Validate(
                module.RecordClassId,
                moduleConfig,
                $"Module Variant '{request.OwnerNode.Id}' config");
            moduleVariant["config"] = moduleConfig;
            var moduleMetadataJson = moduleMetadata.ToJsonString();
            global::Mockups.DesktopEditorShell.Data.AppModuleRepository.ValidateModuleMetadata(
                moduleMetadataJson,
                moduleId,
                module.RecordClassId);

            _commitVariants(connection,
            [
                new(ProjectTreeNodeKind.ComponentVariant, promoted.Component.Id, promoted.MetadataJson),
                new(ProjectTreeNodeKind.ModuleVariant, moduleId, moduleMetadataJson),
            ]);

            return PromotedVariantNode(
                promoted,
                name);
        }
    }

    private ProjectTreeNode PromoteComponentCollectionOverridesToVariant(
        CollectionPromotionRequest request)
    {
        var (ownerComponentId, ownerVariantId, name) =
            RequiredComponentVariantOwner(
                request.OwnerNode,
                request.Name);

        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var ownerComponent = _componentClassRepository.Get(
                connection,
                ownerComponentId);
            var ownerMetadata = ParseJsonObject(
                ownerComponent.MetadataJson);
            var ownerVariant = RequiredEditableComponentVariant(
                ownerMetadata,
                ownerComponentId,
                ownerVariantId);
            var ownerConfig = ownerVariant["config"] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Component Variant '{request.OwnerNode.Id}' has no config.");
            var field = ComponentClassFieldCatalog.Get(
                request.CollectionFieldId);
            var definition = field.StructuredCollection
                ?? throw new InvalidOperationException(
                    $"Component field '{request.CollectionFieldId}' is not a structured collection.");
            var path = field.JsonPath;
            if (path.Length == 0)
                throw new InvalidOperationException($"Component field '{request.CollectionFieldId}' has no config path.");
            var items = JsonPath.Get(ownerConfig, path) as JsonArray
                ?? throw new InvalidOperationException(
                    $"Component field '{request.CollectionFieldId}' must be an array.");
            var item = RequiredCollectionItem(
                items,
                definition,
                request.CollectionAddress,
                request.ItemId);
            var (variantReference, overrides) = ReadBoundary(
                item,
                request.Boundary,
                request.CollectionFieldId);
            if (!OverrideDocumentContract.HasAuthoredValues(overrides))
            {
                throw new InvalidOperationException(
                    "Only authored Overrides can be converted into a Variant.");
            }
            var promoted = PreparePromotedVariant(
                connection,
                variantReference,
                overrides,
                name);

            var finalOwnerMetadata = promoted.Component.Id.Equals(
                    ownerComponentId,
                    StringComparison.Ordinal)
                ? ParseJsonObject(promoted.MetadataJson)
                : ownerMetadata;
            var finalOwnerVariant = RequiredEditableComponentVariant(
                finalOwnerMetadata,
                ownerComponentId,
                ownerVariantId);
            var finalOwnerConfig = finalOwnerVariant["config"] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Component Variant '{request.OwnerNode.Id}' has no config.");
            var finalItems = JsonPath.Get(
                    finalOwnerConfig,
                    path)
                as JsonArray
                ?? throw new InvalidOperationException(
                    $"Component field '{request.CollectionFieldId}' must be an array.");
            var finalItem = RequiredCollectionItem(
                finalItems,
                definition,
                request.CollectionAddress,
                request.ItemId);
            WriteBoundary(
                finalItem,
                request.Boundary,
                promoted.Reference,
                request.CollectionFieldId);
            StructuredCollectionDocumentContract.Validate(
                finalItems,
                definition,
                $"Component field '{request.CollectionFieldId}'");
            var ownerMetadataJson = finalOwnerMetadata.ToJsonString();

            var changes = new List<VariantDocumentChange>();
            if (promoted.Component.Id != ownerComponentId)
                changes.Add(new(ProjectTreeNodeKind.ComponentVariant, promoted.Component.Id, promoted.MetadataJson));
            changes.Add(new(ProjectTreeNodeKind.ComponentVariant, ownerComponentId, ownerMetadataJson));
            _commitVariants(connection, changes);
            return PromotedVariantNode(
                promoted,
                name);
        }
    }

    public bool HasEmbeddedComponentOverrides(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        if (slots.Count == 0)
        {
            return false;
        }
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var config = VariantOwnerConfigForRead(
                connection,
                ownerNode);
            var slot = RequiredEmbeddedSlot(
                config,
                slots);
            var overrides = ComponentVariantSlotDocumentContract.Overrides(
                slot,
                $"Embedded Component slot '{slots[^1].FieldId}'");
            return OverrideDocumentContract.HasAuthoredValues(
                overrides);
        }
    }

    private ProjectTreeNode PromoteEmbeddedOverridesToVariant(
        SlotPathPromotionRequest request) =>
        request.OwnerNode.Kind switch
        {
            ProjectTreeNodeKind.ModuleVariant =>
                PromoteModuleEmbeddedOverridesToVariant(request),
            ProjectTreeNodeKind.ComponentVariant =>
                PromoteComponentEmbeddedOverridesToVariant(request),
            _ => throw new InvalidOperationException(
                "Embedded Component Override promotion requires an editable Variant owner."),
        };

    private ProjectTreeNode PromoteModuleEmbeddedOverridesToVariant(
        SlotPathPromotionRequest request)
    {
        if (request.Slots.Count == 0
            || !VariantReferenceId.TryParse(
                request.OwnerNode.Id,
                out var moduleId,
                out var moduleVariantId))
        {
            throw new InvalidOperationException(
                "Embedded Component Override promotion requires a Module Variant owner and an exact slot path.");
        }
        if (request.OwnerNode.IsLocked)
        {
            throw new InvalidOperationException(
                $"Module Variant '{request.OwnerNode.Name}' is locked.");
        }
        var name = RequiredPromotionName(request.Name);
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var module = _appModuleRepository.GetModule(
                connection,
                moduleId);
            var moduleMetadata = ParseJsonObject(
                module.MetadataJson);
            var moduleVariant = VariantEnvelopeContract.FindSource(
                    VariantEnvelopeContract.RequiredArray(
                        moduleMetadata,
                        "variants",
                        $"Module '{moduleId}'"),
                    moduleVariantId)
                ?? throw new InvalidOperationException(
                    $"Missing Module Variant '{request.OwnerNode.Id}'.");
            if (JsonBool(moduleVariant, ["locked"]))
            {
                throw new InvalidOperationException(
                    $"Module Variant '{request.OwnerNode.Name}' is locked.");
            }
            var moduleConfig = moduleVariant["config"] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Module Variant '{request.OwnerNode.Id}' has no config.");
            var slot = RequiredEmbeddedSlot(
                moduleConfig,
                request.Slots);
            var owner = $"Embedded Component slot '{request.Slots[^1].FieldId}'";
            var overrides = ComponentVariantSlotDocumentContract.Overrides(
                slot,
                owner);
            if (!OverrideDocumentContract.HasAuthoredValues(overrides))
            {
                throw new InvalidOperationException(
                    "Only authored Overrides can be converted into a Variant.");
            }
            var promoted = PreparePromotedVariant(
                connection,
                ComponentVariantSlotDocumentContract.VariantReference(
                    slot,
                    owner),
                overrides,
                name);
            ReplaceEmbeddedSlotReference(
                slot,
                promoted.Reference);
            CurrentModuleConfigContract.Validate(
                module.RecordClassId,
                moduleConfig,
                $"Module Variant '{request.OwnerNode.Id}' config");
            moduleVariant["config"] = moduleConfig;
            var moduleMetadataJson = moduleMetadata.ToJsonString();
            global::Mockups.DesktopEditorShell.Data.AppModuleRepository.ValidateModuleMetadata(
                moduleMetadataJson,
                moduleId,
                module.RecordClassId);

            _commitVariants(connection,
            [
                new(ProjectTreeNodeKind.ComponentVariant, promoted.Component.Id, promoted.MetadataJson),
                new(ProjectTreeNodeKind.ModuleVariant, moduleId, moduleMetadataJson),
            ]);
            return PromotedVariantNode(
                promoted,
                name);
        }
    }

    private ProjectTreeNode PromoteComponentEmbeddedOverridesToVariant(
        SlotPathPromotionRequest request)
    {
        if (request.Slots.Count == 0)
        {
            throw new InvalidOperationException(
                "Embedded Component Override promotion requires an exact slot path.");
        }
        var (ownerComponentId, ownerVariantId, name) =
            RequiredComponentVariantOwner(
                request.OwnerNode,
                request.Name);
        lock (WriteGate)
        {
            using var connection = OpenConnection();
            var ownerComponent = _componentClassRepository.Get(
                connection,
                ownerComponentId);
            var ownerMetadata = ParseJsonObject(
                ownerComponent.MetadataJson);
            var ownerVariant = RequiredEditableComponentVariant(
                ownerMetadata,
                ownerComponentId,
                ownerVariantId);
            var ownerConfig = ownerVariant["config"] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Component Variant '{request.OwnerNode.Id}' has no config.");
            var slot = RequiredEmbeddedSlot(
                ownerConfig,
                request.Slots);
            var owner = $"Embedded Component slot '{request.Slots[^1].FieldId}'";
            var overrides = ComponentVariantSlotDocumentContract.Overrides(
                slot,
                owner);
            if (!OverrideDocumentContract.HasAuthoredValues(overrides))
            {
                throw new InvalidOperationException(
                    "Only authored Overrides can be converted into a Variant.");
            }
            var promoted = PreparePromotedVariant(
                connection,
                ComponentVariantSlotDocumentContract.VariantReference(
                    slot,
                    owner),
                overrides,
                name);
            var finalOwnerMetadata = promoted.Component.Id.Equals(
                    ownerComponentId,
                    StringComparison.Ordinal)
                ? ParseJsonObject(promoted.MetadataJson)
                : ownerMetadata;
            var finalOwnerVariant = RequiredEditableComponentVariant(
                finalOwnerMetadata,
                ownerComponentId,
                ownerVariantId);
            var finalOwnerConfig = finalOwnerVariant["config"] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Component Variant '{request.OwnerNode.Id}' has no config.");
            ReplaceEmbeddedSlotReference(
                RequiredEmbeddedSlot(
                    finalOwnerConfig,
                    request.Slots),
                promoted.Reference);
            var ownerMetadataJson = finalOwnerMetadata.ToJsonString();

            var changes = new List<VariantDocumentChange>();
            if (promoted.Component.Id != ownerComponentId)
                changes.Add(new(ProjectTreeNodeKind.ComponentVariant, promoted.Component.Id, promoted.MetadataJson));
            changes.Add(new(ProjectTreeNodeKind.ComponentVariant, ownerComponentId, ownerMetadataJson));
            _commitVariants(connection, changes);
            return PromotedVariantNode(
                promoted,
                name);
        }
    }

    private static (string ComponentClassId, string VariantId, string Name)
        RequiredComponentVariantOwner(
            ProjectTreeNode ownerNode,
            string requestedName)
    {
        if (ownerNode.Kind != ProjectTreeNodeKind.ComponentVariant
            || !VariantReferenceId.TryParse(
                ownerNode.Id,
                out var componentClassId,
                out var variantId))
        {
            throw new InvalidOperationException(
                "Component Override promotion requires a Component Variant owner.");
        }
        if (ownerNode.IsLocked)
        {
            throw new InvalidOperationException(
                $"Component Variant '{ownerNode.Name}' is locked.");
        }
        var name = requestedName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                "Variant name cannot be empty.");
        }
        return (componentClassId, variantId, name);
    }

    private JsonObject VariantOwnerConfigForRead(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        ProjectTreeNode ownerNode)
    {
        if (!VariantReferenceId.TryParse(
                ownerNode.Id,
                out var ownerId,
                out var variantId))
        {
            throw new InvalidOperationException(
                $"Invalid Variant owner id '{ownerNode.Id}'.");
        }
        JsonObject metadata;
        string owner;
        if (ownerNode.Kind == ProjectTreeNodeKind.ModuleVariant)
        {
            var module = _appModuleRepository.GetModule(
                connection,
                ownerId);
            metadata = ParseJsonObject(module.MetadataJson);
            owner = $"Module '{ownerId}'";
        }
        else if (ownerNode.Kind == ProjectTreeNodeKind.ComponentVariant)
        {
            var component = _componentClassRepository.Get(
                connection,
                ownerId);
            metadata = ParseJsonObject(component.MetadataJson);
            owner = $"Component class '{ownerId}'";
        }
        else
        {
            throw new InvalidOperationException(
                "Embedded Component Overrides require a Variant owner.");
        }
        var variant = VariantEnvelopeContract.FindSource(
                VariantEnvelopeContract.RequiredArray(
                    metadata,
                    "variants",
                    owner),
                variantId)
            ?? throw new InvalidOperationException(
                $"Missing Variant '{ownerNode.Id}'.");
        return variant["config"] as JsonObject
            ?? throw new InvalidOperationException(
                $"Variant '{ownerNode.Id}' has no config.");
    }

    private static JsonObject RequiredEmbeddedSlot(
        JsonObject rootConfig,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        if (slots.Count == 0)
        {
            throw new InvalidOperationException(
                "An embedded Component boundary requires an exact slot path.");
        }
        var config = rootConfig;
        JsonObject? slotNode = null;
        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            slotNode = JsonPath.Get(
                    config,
                    slot.SlotPath)
                as JsonObject
                ?? throw new InvalidOperationException(
                    $"Embedded Component slot '{slot.FieldId}' must be an object.");
            if (index < slots.Count - 1)
            {
                config = ComponentVariantSlotDocumentContract.Overrides(
                    slotNode,
                    $"Embedded Component slot '{slot.FieldId}'");
            }
        }
        return slotNode!;
    }

    private static void ReplaceEmbeddedSlotReference(
        JsonObject slot,
        string variantReference)
    {
        slot["variantReference"] = variantReference;
        slot["overrides"] = new JsonObject();
    }

    private static string RequiredPromotionName(string requestedName)
    {
        var name = requestedName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException(
                "Variant name cannot be empty.");
        }
        return name;
    }

    private JsonObject RequiredEditableComponentVariant(
        JsonObject metadata,
        string componentClassId,
        string variantId)
    {
        var variant = VariantEnvelopeContract.FindSource(
                VariantEnvelopeContract.RequiredArray(
                    metadata,
                    "variants",
                    $"Component class '{componentClassId}'"),
                variantId)
            ?? throw new InvalidOperationException(
                $"Missing Component Variant '{componentClassId}::variant::{variantId}'.");
        if (IsVariantLockedForEditing(
                componentClassId,
                variantId,
                JsonBool(variant, ["locked"])))
        {
            throw new InvalidOperationException(
                $"Component Variant '{variantId}' is locked.");
        }
        return variant;
    }

    private PreparedPromotedVariant PreparePromotedVariant(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        string variantReference,
        JsonObject overrides,
        string name)
    {
        if (!VariantReferenceId.TryParse(
                variantReference,
                out var componentClassId,
                out var sourceVariantId))
        {
            throw new InvalidOperationException(
                $"Invalid Component Variant reference '{variantReference}'.");
        }
        var component = _componentClassRepository.Get(
            connection,
            componentClassId);
        var componentMetadata = ParseJsonObject(
            component.MetadataJson);
        var componentVariants = VariantEnvelopeContract.RequiredArray(
            componentMetadata,
            "variants",
            $"Component class '{componentClassId}'");
        var sourceVariant = VariantEnvelopeContract.FindSource(
                componentVariants,
                sourceVariantId)
            ?? throw new InvalidOperationException(
                $"Missing Component Variant '{variantReference}'.");
        var config = (sourceVariant["config"] as JsonObject
                ?? throw new InvalidOperationException(
                    $"Component Variant '{variantReference}' has no config."))
            .DeepClone()
            .AsObject();
        ComponentConfigOverrideMerger.MergeInto(
            config,
            overrides);
        ApplyComponentInputBindingsProjections(
            connection,
            config,
            ComponentInputBindingsProjectionCatalog.ComponentOwners());
        CurrentComponentConfigContract.Validate(
            component.ComponentType,
            config,
            $"Promoted Component Variant '{componentClassId}' config");
        ValidateDeclaredComponentVariantReferences(
            connection,
            config);
        var variantId = VariantEnvelopeContract.UniqueId(
            componentVariants,
            name);
        componentVariants.Add(
            VariantEnvelopeContract.CreateSource(
                variantId,
                name,
                config));
        var metadataJson = componentMetadata.ToJsonString();
        var validatedMetadata =
            global::Mockups.DesktopEditorShell.Data.ComponentClassRepository.ValidateMetadata(
                metadataJson,
                componentClassId);
        global::Mockups.DesktopEditorShell.Data.ComponentClassRepository.ValidateVariantConfigs(
            component.ComponentType,
            component.RecordClassId,
            validatedMetadata,
            componentClassId);
        return new PreparedPromotedVariant(
            component,
            metadataJson,
            VariantReferenceId.Format(
                componentClassId,
                variantId));
    }

    private static ProjectTreeNode PromotedVariantNode(
        PreparedPromotedVariant promoted,
        string name)
    {
        var parent = new ProjectTreeNode(
            ProjectTreeNodeKind.ComponentClass,
            promoted.Component.Id,
            promoted.Component.Name,
            promoted.Component.Notes,
            promoted.Component.RecordClassId);
        return new ProjectTreeNode(
            ProjectTreeNodeKind.ComponentVariant,
            promoted.Reference,
            name,
            "Component variant",
            ProjectTreeNode.DefaultRecordClassId(
                ProjectTreeNodeKind.ComponentVariant),
            parent);
    }

    private sealed record PreparedPromotedVariant(
        ComponentClassDefinitionRecord Component,
        string MetadataJson,
        string Reference);

    private static JsonObject RequiredCollectionItem(
        JsonArray root,
        RuntimeInputCollectionDefinition rootDefinition,
        StructuredCollectionAddress address,
        string itemId)
    {
        if (!address.RootStorageJsonKey.Equals(
                rootDefinition.JsonKey,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Structured collection address '{address.RootStorageJsonKey}' does not match '{rootDefinition.JsonKey}'.");
        }
        var definition = rootDefinition;
        var collection = root;
        var collectionKey = rootDefinition.JsonKey;
        for (var ownerIndex = 0;
             ownerIndex < address.Owners.Count;
             ownerIndex++)
        {
            var owner = address.Owners[ownerIndex];
            if (!owner.CollectionJsonKey.Equals(
                    collectionKey,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Structured collection address expected '{collectionKey}' but received '{owner.CollectionJsonKey}'.");
            }
            var ownerItem = RequiredItem(
                collection,
                owner.ItemId,
                collectionKey);
            var nextKey = ownerIndex == address.Owners.Count - 1
                ? address.CollectionJsonKey
                : address.Owners[ownerIndex + 1].CollectionJsonKey;
            var nestedField = definition.Fields.SingleOrDefault(
                candidate => candidate.JsonKey.Equals(
                    nextKey,
                    StringComparison.Ordinal)
                    && candidate.StructuredCollection is not null)
                ?? throw new InvalidOperationException(
                    $"Structured collection '{definition.Id}' has no nested collection '{nextKey}'.");
            definition = nestedField.StructuredCollection!;
            collection = ownerItem[nextKey] as JsonArray
                ?? throw new InvalidOperationException(
                    $"Structured collection item '{owner.ItemId}' requires array '{nextKey}'.");
            collectionKey = nextKey;
        }
        if (!collectionKey.Equals(
                address.CollectionJsonKey,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Structured collection address resolved '{collectionKey}' instead of '{address.CollectionJsonKey}'.");
        }
        return RequiredItem(
            collection,
            itemId,
            definition.Id);
    }

    private static JsonObject RequiredItem(
        JsonArray items,
        string itemId,
        string owner) =>
        items.OfType<JsonObject>().SingleOrDefault(candidate =>
            JsonPath.RequiredString(
                    candidate,
                    "id",
                    $"Structured collection '{owner}'")
                .Equals(itemId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"Structured collection '{owner}' has no item '{itemId}'.");

    private static (string VariantReference, JsonObject Overrides)
        ReadBoundary(
            JsonObject item,
            ComponentOverridePromotionBoundary boundary,
            string owner)
    {
        if (!string.IsNullOrWhiteSpace(boundary.SlotJsonKey))
        {
            var slot = JsonPath.RequiredObject(
                item,
                boundary.SlotJsonKey,
                owner);
            return (
                ComponentVariantSlotDocumentContract.VariantReference(
                    slot,
                    owner),
                ComponentVariantSlotDocumentContract.Overrides(
                    slot,
                    owner));
        }
        return (
            JsonPath.RequiredString(
                item,
                boundary.VariantReferenceJsonKey,
                owner),
            JsonPath.RequiredObject(
                item,
                boundary.OverridesJsonKey,
                owner));
    }

    private static void WriteBoundary(
        JsonObject item,
        ComponentOverridePromotionBoundary boundary,
        string variantReference,
        string owner)
    {
        if (!string.IsNullOrWhiteSpace(boundary.SlotJsonKey))
        {
            item[boundary.SlotJsonKey] =
                ComponentVariantSlotDocumentContract.Create(
                    variantReference,
                    new JsonObject(),
                    owner);
            return;
        }
        item[boundary.VariantReferenceJsonKey] =
            variantReference;
        item[boundary.OverridesJsonKey] =
            new JsonObject();
    }

    private sealed record FieldPromotionRequest(
        ProjectTreeNode OwnerNode,
        string FieldId,
        string Name);

    private sealed record CollectionPromotionRequest(
        ProjectTreeNode OwnerNode,
        string CollectionFieldId,
        StructuredCollectionAddress CollectionAddress,
        string ItemId,
        ComponentOverridePromotionBoundary Boundary,
        string Name);

    private sealed record SlotPathPromotionRequest(
        ProjectTreeNode OwnerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> Slots,
        string Name);
}
