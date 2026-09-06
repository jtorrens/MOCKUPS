using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ExternalMediaReplacementCoordinator
{
    private readonly EditorPathBrowser _paths;
    private readonly EditorFieldValueRouter _fields;
    private readonly ComponentClassFieldValueService _componentFields;
    private readonly RuntimeInputOwnerDocumentStore _ownerDocuments;
    private readonly RuntimeInputInstanceDocumentStore _instanceDocuments;
    private readonly EditorOperationCoordinator _operations;
    private readonly IExternalMediaUsageQuery _usage;
    private readonly Func<string, ProjectTreeNode?> _findNode;
    private readonly Func<string, string, Task> _showInfo;
    private readonly Action _authoredValuesChanged;

    public ExternalMediaReplacementCoordinator(
        EditorPathBrowser paths,
        EditorFieldValueRouter fields,
        ComponentClassFieldValueService componentFields,
        IRuntimeInputOwnerStore runtimeInputOwners,
        IRuntimeInputInstanceStore runtimeInputInstances,
        IModuleInstanceAnimationStore animation,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery themeTokens,
        EditorOperationCoordinator operations,
        IExternalMediaUsageQuery usage,
        Func<string, ProjectTreeNode?> findNode,
        Func<string, string, Task> showInfo,
        Action authoredValuesChanged)
    {
        _paths = paths;
        _fields = fields;
        _componentFields = componentFields;
        _ownerDocuments = new RuntimeInputOwnerDocumentStore(
            runtimeInputOwners,
            timeline,
            operations);
        _instanceDocuments = new RuntimeInputInstanceDocumentStore(
            runtimeInputInstances,
            animation,
            timeline,
            themeTokens,
            operations);
        _operations = operations;
        _usage = usage;
        _findNode = findNode;
        _showInfo = showInfo;
        _authoredValuesChanged = authoredValuesChanged;
    }

    public async Task<IReadOnlyList<ExternalMediaUsageDetail>?> ReplaceAsync(
        ExternalMediaUsageDetail usage)
    {
        var replacement = await _paths.BrowsePath(
            usage.AuthoredPath,
            usage.ValueKind);
        if (string.IsNullOrWhiteSpace(replacement)) return null;

        try
        {
            var node = _findNode(usage.SourceNodeId)
                ?? throw new InvalidOperationException(
                    $"Could not find {usage.SourceTypeLabel} '{usage.SourceName}'.");
            if (usage.AuthoringSurface == ExternalMediaAuthoringSurface.Editor)
            {
                await ReplaceEditorValueAsync(node, usage, replacement);
            }
            else if (node.Kind == ProjectTreeNodeKind.ModuleInstance)
            {
                await ReplaceProductionRuntimeValueAsync(node, usage, replacement);
            }
            else
            {
                await ReplaceDesignPreviewValueAsync(node, usage, replacement);
            }
            _authoredValuesChanged();
            return await _operations.ExecuteAsync(
                () => _usage.GetExternalMediaUsageDetails(usage.ProjectId));
        }
        catch (Exception exception)
        {
            await _showInfo(
                "Media not replaced",
                exception.Message);
            return null;
        }
    }

    private async Task ReplaceEditorValueAsync(
        ProjectTreeNode node,
        ExternalMediaUsageDetail usage,
        string replacement)
    {
        if (usage.ItemId.Length == 0)
        {
            await _operations.ExecuteAsync(() =>
            {
                if (usage.SlotFieldIds.Count == 0)
                {
                    var directField = _fields.Create(node, usage.DeclaredFieldId);
                    RequireEditable(directField, usage);
                    _fields.Persist(node, usage.DeclaredFieldId, replacement);
                    return;
                }

                var slots = usage.SlotFieldIds
                    .Select(EmbeddedComponentSlotCatalog.Get)
                    .ToArray();
                var embeddedField = _componentFields.CreateEmbeddedFieldValue(
                    node,
                    slots,
                    usage.DeclaredFieldId);
                RequireEditable(embeddedField, usage);
                _componentFields.CommitEmbeddedFieldValue(
                    node,
                    slots,
                    usage.DeclaredFieldId,
                    replacement);
            });
            return;
        }

        await _operations.ExecuteAsync(() =>
        {
            var slots = usage.SlotFieldIds
                .Select(EmbeddedComponentSlotCatalog.Get)
                .ToArray();
            var current = slots.Length == 0
                ? _fields.Create(node, usage.FieldId)
                : _componentFields.CreateEmbeddedFieldValue(
                    node,
                    slots,
                    usage.FieldId);
            RequireEditable(current, usage);
            var collection = current.Definition.StructuredCollection
                ?? throw new InvalidOperationException(
                    $"Field '{usage.FieldId}' is not a declared structured collection.");
            var items = JsonNode.Parse(current.Value) as JsonArray
                ?? throw new InvalidOperationException(
                    $"Field '{usage.FieldId}' must contain a collection array.");
            var matches = ReplaceInCollection(
                items,
                collection,
                usage,
                replacement);
            if (matches != 1)
            {
                throw new InvalidOperationException(
                    $"Collection field '{usage.FieldId}' has {matches} exact media-field matches.");
            }
            var serialized = items.ToJsonString();
            if (slots.Length == 0)
            {
                _fields.Persist(node, usage.FieldId, serialized);
            }
            else
            {
                _componentFields.CommitEmbeddedFieldValue(
                    node,
                    slots,
                    usage.FieldId,
                    serialized);
            }
        });
    }

    private async Task ReplaceDesignPreviewValueAsync(
        ProjectTreeNode node,
        ExternalMediaUsageDetail usage,
        string replacement)
    {
        var source = _ownerDocuments.Load(node);
        var preview = DesignPreviewTestValues.Parse(source.RuntimePreviewJson);
        var config = DesignPreviewTestValues.Parse(source.ConfigJson);
        if (usage.IsRuntimeDefault)
        {
            ReplaceRuntimeDefault(preview, usage, replacement);
            await _ownerDocuments.SaveDesignPreviewJsonAsync(
                source,
                preview.ToJsonString());
            return;
        }
        if (usage.ItemId.Length == 0)
        {
            var input = RuntimeInputDefinitionReader.ReadInputs(preview, config)
                .SingleOrDefault((candidate) => candidate.Id.Equals(
                    usage.FieldId,
                    StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"Runtime Input '{usage.FieldId}' is unavailable.");
            var nextValue = RuntimeReplacementValue(
                preview,
                input,
                usage,
                replacement);
            var testValues = preview["testValues"] as JsonObject;
            if (testValues is null)
            {
                testValues = new JsonObject();
                preview["testValues"] = testValues;
            }
            testValues[input.JsonKey] = nextValue;
        }
        else
        {
            var collections = RuntimeInputDefinitionReader.ReadCollections(
                preview,
                config);
            var matches = collections
                .Select((collection) => TryReplaceDesignCollection(
                    preview,
                    collection,
                    usage,
                    replacement))
                .Where((candidate) => candidate)
                .Count();
            if (matches != 1)
            {
                throw new InvalidOperationException(
                    $"Runtime item '{usage.ItemId}' has {matches} exact media-field matches.");
            }
        }
        await _ownerDocuments.SaveDesignPreviewJsonAsync(
            source,
            preview.ToJsonString());
    }

    private static void ReplaceRuntimeDefault(
        JsonObject preview,
        ExternalMediaUsageDetail usage,
        string replacement)
    {
        if (usage.ItemId.Length > 0)
        {
            throw new InvalidOperationException(
                "A collection item cannot own a root Runtime Input default.");
        }
        var inputs = preview["inputs"] as JsonArray
            ?? throw new InvalidOperationException(
                "Design Preview requires its declared Runtime Input array.");
        var definitions = inputs.OfType<JsonObject>()
            .Where((definition) => definition["id"] is JsonValue value
                && value.TryGetValue<string>(out var id)
                && id.Equals(usage.FieldId, StringComparison.Ordinal))
            .ToArray();
        if (definitions.Length != 1)
        {
            throw new InvalidOperationException(
                $"Runtime Input '{usage.FieldId}' has {definitions.Length} exact default definitions.");
        }
        var definition = definitions[0];
        if (usage.FieldId.Equals(usage.DeclaredFieldId, StringComparison.Ordinal))
        {
            definition["defaultValue"] = replacement;
            return;
        }
        var defaultValues = definition["defaultValue"] as JsonObject
            ?? throw new InvalidOperationException(
                $"Runtime Input '{usage.FieldId}' default must be an object.");
        if (defaultValues[usage.DeclaredJsonKey] is not JsonValue current
            || !current.TryGetValue<string>(out var path)
            || !path.Equals(usage.AuthoredPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Runtime Input '{usage.FieldId}' no longer contains the selected media default.");
        }
        defaultValues[usage.DeclaredJsonKey] = replacement;
    }

    private async Task ReplaceProductionRuntimeValueAsync(
        ProjectTreeNode node,
        ExternalMediaUsageDetail usage,
        string replacement)
    {
        var source = _ownerDocuments.Load(node);
        var preview = DesignPreviewTestValues.Parse(source.RuntimePreviewJson);
        var config = DesignPreviewTestValues.Parse(source.ConfigJson);
        if (usage.ItemId.Length == 0)
        {
            var input = RuntimeInputDefinitionReader.ReadInputs(preview, config)
                .SingleOrDefault((candidate) => candidate.Id.Equals(
                    usage.FieldId,
                    StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"Runtime Input '{usage.FieldId}' is unavailable.");
            await _instanceDocuments.UpdateRuntimeValueAsync(
                node.Id,
                input.JsonKey,
                RuntimeReplacementValue(
                    preview,
                    input,
                    usage,
                    replacement));
            return;
        }

        var matches = new List<ProductionCollectionReplacement>();
        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(
                     preview,
                     config))
        {
            var rootItems = preview[collection.StorageJsonKey] as JsonArray;
            if (rootItems is null) continue;
            FindProductionCollectionReplacement(
                rootItems,
                collection,
                StructuredCollectionAddress.Root(collection.StorageJsonKey),
                usage,
                replacement,
                matches);
        }
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Runtime item '{usage.ItemId}' has {matches.Count} exact media-field matches.");
        }
        var match = matches[0];
        await _instanceDocuments.UpdateCollectionValueAsync(
            node.Id,
            match.Address,
            usage.ItemId,
            match.FieldJsonKey,
            match.Value);
    }

    private static JsonNode? RuntimeReplacementValue(
        JsonObject values,
        ComponentInputDefinition input,
        ExternalMediaUsageDetail usage,
        string replacement)
    {
        if (usage.FieldId.Equals(usage.DeclaredFieldId, StringComparison.Ordinal))
        {
            RequireMediaKind(input.ValueKind, usage);
            return DesignPreviewTestValues.ValueNode(input, replacement);
        }
        if (input.ValueKind != ValueKind.ComponentInputBindings)
        {
            throw new InvalidOperationException(
                $"Runtime Input '{input.Id}' does not declare nested media bindings.");
        }
        var document = JsonNode.Parse(
                DesignPreviewTestValues.Value(values, input)) as JsonObject
            ?? throw new InvalidOperationException(
                $"Runtime Input '{input.Id}' value must be an object.");
        if (document[usage.DeclaredJsonKey] is not JsonValue current
            || !current.TryGetValue<string>(out var path)
            || !path.Equals(usage.AuthoredPath, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Runtime Input '{input.Id}' no longer contains the selected media path.");
        }
        document[usage.DeclaredJsonKey] = replacement;
        return document;
    }

    private bool TryReplaceDesignCollection(
        JsonObject preview,
        RuntimeInputCollectionDefinition collection,
        ExternalMediaUsageDetail usage,
        string replacement)
    {
        var items = new JsonArray(
            DesignPreviewTestValues.CollectionItems(preview, collection)
                .Select((item) => (JsonNode?)item.DeepClone())
                .ToArray());
        var matches = new List<ProductionCollectionReplacement>();
        FindProductionCollectionReplacement(
            items,
            collection,
            StructuredCollectionAddress.Root(collection.JsonKey),
            usage,
            replacement,
            matches);
        if (matches.Count == 0) return false;
        if (matches.Count != 1)
        {
            throw new InvalidOperationException(
                $"Collection '{collection.Id}' has {matches.Count} exact media-field matches.");
        }
        var testValues = preview["testValues"] as JsonObject;
        if (testValues is null)
        {
            testValues = new JsonObject();
            preview["testValues"] = testValues;
        }
        if (string.IsNullOrWhiteSpace(collection.SourceCollectionJsonKey))
        {
            testValues[collection.JsonKey] = items;
        }
        else
        {
            var match = matches[0];
            SetSparseCollectionValue(
                testValues,
                match.Address,
                usage.ItemId,
                match.FieldJsonKey,
                match.Value);
        }
        return true;
    }

    private static void SetSparseCollectionValue(
        JsonObject testValues,
        StructuredCollectionAddress address,
        string itemId,
        string fieldJsonKey,
        JsonNode? value)
    {
        var collection = ArrayForSparseWrite(
            testValues,
            address.RootStorageJsonKey);
        for (var index = 0; index < address.Owners.Count; index++)
        {
            var owner = address.Owners[index];
            var ownerItem = ItemForSparseWrite(collection, owner.ItemId);
            var nextKey = index + 1 < address.Owners.Count
                ? address.Owners[index + 1].CollectionJsonKey
                : address.CollectionJsonKey;
            collection = ArrayForSparseWrite(ownerItem, nextKey);
        }
        ItemForSparseWrite(collection, itemId)[fieldJsonKey] = value?.DeepClone();
    }

    private static JsonArray ArrayForSparseWrite(
        JsonObject owner,
        string jsonKey)
    {
        if (owner[jsonKey] is JsonArray current) return current;
        var created = new JsonArray();
        owner[jsonKey] = created;
        return created;
    }

    private static JsonObject ItemForSparseWrite(
        JsonArray items,
        string itemId)
    {
        var matches = items.OfType<JsonObject>()
            .Where((item) => item["id"] is JsonValue value
                && value.TryGetValue<string>(out var id)
                && id.Equals(itemId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length > 1)
        {
            throw new InvalidOperationException(
                $"Sparse collection override contains duplicate item id '{itemId}'.");
        }
        if (matches.Length == 1) return matches[0];
        var created = new JsonObject { ["id"] = itemId };
        items.Add(created);
        return created;
    }

    private static int ReplaceInCollection(
        JsonArray items,
        RuntimeInputCollectionDefinition collection,
        ExternalMediaUsageDetail usage,
        string replacement)
    {
        var matches = 0;
        foreach (var item in items.OfType<JsonObject>())
        {
            var itemId = JsonPath.RequiredString(
                item,
                "id",
                $"Collection '{collection.Id}' item");
            if (itemId.Equals(usage.ItemId, StringComparison.Ordinal))
            {
                matches += ReplaceDeclaredItemValue(
                    item,
                    collection,
                    usage,
                    replacement,
                    out _,
                    out _);
            }
            foreach (var field in collection.Fields.Where((candidate) =>
                         candidate.ValueKind == ValueKind.StructuredCollection
                         && candidate.StructuredCollection is not null))
            {
                if (item[field.JsonKey] is JsonArray nested)
                {
                    matches += ReplaceInCollection(
                        nested,
                        field.StructuredCollection!,
                        usage,
                        replacement);
                }
            }
        }
        return matches;
    }

    private static void FindProductionCollectionReplacement(
        JsonArray items,
        RuntimeInputCollectionDefinition collection,
        StructuredCollectionAddress address,
        ExternalMediaUsageDetail usage,
        string replacement,
        ICollection<ProductionCollectionReplacement> matches)
    {
        foreach (var item in items.OfType<JsonObject>())
        {
            var itemId = JsonPath.RequiredString(
                item,
                "id",
                $"Collection '{collection.Id}' item");
            if (itemId.Equals(usage.ItemId, StringComparison.Ordinal)
                && ReplaceDeclaredItemValue(
                    item,
                    collection,
                    usage,
                    replacement,
                    out var fieldJsonKey,
                    out var value) == 1)
            {
                matches.Add(new ProductionCollectionReplacement(
                    address,
                    fieldJsonKey,
                    value));
            }
            foreach (var field in collection.Fields.Where((candidate) =>
                         candidate.ValueKind == ValueKind.StructuredCollection
                         && candidate.StructuredCollection is not null))
            {
                if (item[field.JsonKey] is not JsonArray nested) continue;
                var nestedDefinition = field.StructuredCollection!;
                FindProductionCollectionReplacement(
                    nested,
                    nestedDefinition,
                    new StructuredCollectionAddress(
                        address.RootStorageJsonKey,
                        [
                            .. address.Owners,
                            new StructuredCollectionOwnerSegment(
                                address.CollectionJsonKey,
                                itemId),
                        ],
                        nestedDefinition.JsonKey),
                    usage,
                    replacement,
                    matches);
            }
        }
    }

    private static int ReplaceDeclaredItemValue(
        JsonObject item,
        RuntimeInputCollectionDefinition collection,
        ExternalMediaUsageDetail usage,
        string replacement,
        out string fieldJsonKey,
        out JsonNode? value)
    {
        fieldJsonKey = "";
        value = null;
        var direct = collection.Fields.Where((field) =>
            field.Id.Equals(usage.DeclaredFieldId, StringComparison.Ordinal)
            && IsMediaKind(field.ValueKind)).ToArray();
        if (direct.Length == 1)
        {
            fieldJsonKey = direct[0].JsonKey;
            value = DesignPreviewTestValues.ValueNode(direct[0], replacement);
            item[fieldJsonKey] = value?.DeepClone();
            return 1;
        }

        if (collection.ComponentItems is not { } componentItems
            || item[componentItems.InputsJsonKey] is not JsonObject inputs)
        {
            return 0;
        }
        if (inputs[usage.DeclaredJsonKey] is not JsonValue scalar
            || !scalar.TryGetValue<string>(out var currentValue)
            || !currentValue.Equals(usage.AuthoredPath, StringComparison.Ordinal))
        {
            return 0;
        }
        inputs[usage.DeclaredJsonKey] = replacement;
        fieldJsonKey = componentItems.InputsJsonKey;
        value = inputs.DeepClone();
        item[fieldJsonKey] = value.DeepClone();
        return 1;
    }

    private static void RequireEditable(
        FieldValue value,
        ExternalMediaUsageDetail usage)
    {
        if (!value.Definition.IsEditable)
        {
            throw new InvalidOperationException(
                $"'{usage.SystemItem}' is locked or read-only.");
        }
        RequireMediaKind(usage.ValueKind, usage);
    }

    private static void RequireMediaKind(
        ValueKind valueKind,
        ExternalMediaUsageDetail usage)
    {
        if (!IsMediaKind(valueKind))
        {
            throw new InvalidOperationException(
                $"'{usage.SystemItem}' is not a declared media field.");
        }
    }

    private static bool IsMediaKind(ValueKind valueKind) =>
        valueKind is ValueKind.ImageFilePath
            or ValueKind.MediaFilePath
            or ValueKind.MediaDirectoryPath
            or ValueKind.VideoFilePath;

    private sealed record ProductionCollectionReplacement(
        StructuredCollectionAddress Address,
        string FieldJsonKey,
        JsonNode? Value);
}
