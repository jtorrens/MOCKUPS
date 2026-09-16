using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    public ProjectTreeNode PromoteModuleFieldOverridesToVariant(
        ComponentOverrideFieldPromotionRequest request)
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

            using var transaction = connection.BeginTransaction();
            _context.Execute(
                connection,
                transaction,
                "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", promoted.Component.Id),
                ("$metadataJson", promoted.MetadataJson));
            _context.Execute(
                connection,
                transaction,
                "UPDATE modules SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", moduleId),
                ("$metadataJson", moduleMetadataJson));
            transaction.Commit();
            return PromotedVariantNode(
                promoted,
                name);
        }
    }

    public ProjectTreeNode PromoteModuleCollectionOverridesToVariant(
        ComponentOverridePromotionRequest request)
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

            using var transaction = connection.BeginTransaction();
            _context.Execute(
                connection,
                transaction,
                "UPDATE component_classes SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", promoted.Component.Id),
                ("$metadataJson", promoted.MetadataJson));
            _context.Execute(
                connection,
                transaction,
                "UPDATE modules SET metadata_json = $metadataJson WHERE id = $id",
                ("$id", moduleId),
                ("$metadataJson", moduleMetadataJson));
            transaction.Commit();

            return PromotedVariantNode(
                promoted,
                name);
        }
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
}
