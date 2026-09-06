using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ExternalMediaUsageService : IExternalMediaUsageQuery
{
    private static readonly HashSet<ValueKind> MediaValueKinds =
    [
        ValueKind.ImageFilePath,
        ValueKind.MediaFilePath,
        ValueKind.MediaDirectoryPath,
        ValueKind.VideoFilePath,
    ];

    private readonly SqliteProjectContext _context;

    public ExternalMediaUsageService(SqliteProjectContext context)
    {
        _context = context;
    }

    public IReadOnlyList<ExternalMediaUsageDetail> GetExternalMediaUsageDetails(
        string projectId)
    {
        using var connection = _context.OpenConnection();
        var mediaRoot = ProjectMediaRoot(connection, projectId);
        var components = ReadComponents(connection, projectId);
        var modules = ReadModules(connection, projectId);
        var componentIndex = components
            .SelectMany((component) => component.Variants.Select(
                (variant) => new ComponentVariantOwner(component, variant)))
            .ToDictionary(
                (owner) => owner.Variant.Reference,
                StringComparer.Ordinal);
        var usages = new List<ExternalMediaUsageDetail>();

        var projectRoot = _context.ProjectPaths.ProjectRoot;
        AddRecordUsages(connection, projectId, projectRoot, mediaRoot, usages);
        AddComponentUsages(components, componentIndex, projectRoot, mediaRoot, usages);
        AddModuleUsages(modules, componentIndex, projectRoot, mediaRoot, usages);
        AddScreenUsages(
            connection,
            projectId,
            modules,
            componentIndex,
            projectRoot,
            mediaRoot,
            usages);

        return usages
            .GroupBy(UsageIdentity, StringComparer.Ordinal)
            .Select((group) => group.First())
            .OrderBy((usage) => usage.SystemItem, StringComparer.OrdinalIgnoreCase)
            .ThenBy((usage) => usage.AbsoluteDirectoryPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy((usage) => usage.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AddRecordUsages(
        SqliteConnection connection,
        string projectId,
        string projectRoot,
        string mediaRoot,
        ICollection<ExternalMediaUsageDetail> usages)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT id, name, record_class_id, config_json FROM apps WHERE project_id = $projectId";
            command.Parameters.AddWithValue("$projectId", projectId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var source = new SourceContext(
                    projectId,
                    projectRoot,
                    reader.GetString(0),
                    ProjectTreeNodeKind.App,
                    reader.GetString(2),
                    "App",
                    reader.GetString(1),
                    ReferenceUsageScope.Design,
                    ExternalMediaAuthoringSurface.Editor);
                var config = RequiredObject(reader, 3, $"App '{source.NodeId}' config_json");
                AddPath(usages, source, "app.wallpaper.images.light.filePath", "app.wallpaper.images.light.filePath", "filePath", "Wallpaper · Light", JsonPath.String(config, new[] { "wallpaper", "images", "light", "filePath" }), ValueKind.ImageFilePath, mediaRoot);
                AddPath(usages, source, "app.wallpaper.images.dark.filePath", "app.wallpaper.images.dark.filePath", "filePath", "Wallpaper · Dark", JsonPath.String(config, new[] { "wallpaper", "images", "dark", "filePath" }), ValueKind.ImageFilePath, mediaRoot);
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT id, display_name, metadata_json FROM actors WHERE project_id = $projectId";
            command.Parameters.AddWithValue("$projectId", projectId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var source = new SourceContext(
                    projectId,
                    projectRoot,
                    reader.GetString(0),
                    ProjectTreeNodeKind.Actor,
                    ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Actor),
                    "Actor",
                    reader.GetString(1),
                    ReferenceUsageScope.Production,
                    ExternalMediaAuthoringSurface.Editor);
                var metadata = RequiredObject(reader, 2, $"Actor '{source.NodeId}' metadata_json");
                AddPath(usages, source, "actor.wallpaper.images.light.filePath", "actor.wallpaper.images.light.filePath", "filePath", "Wallpaper · Light", JsonPath.String(metadata, new[] { "wallpaper", "images", "light", "filePath" }), ValueKind.ImageFilePath, mediaRoot);
                AddPath(usages, source, "actor.wallpaper.images.dark.filePath", "actor.wallpaper.images.dark.filePath", "filePath", "Wallpaper · Dark", JsonPath.String(metadata, new[] { "wallpaper", "images", "dark", "filePath" }), ValueKind.ImageFilePath, mediaRoot);
                AddPath(usages, source, "actor.avatar.filePath", "actor.avatar.filePath", "filePath", "Avatar image", JsonPath.String(metadata, new[] { "avatar", "filePath" }), ValueKind.ImageFilePath, mediaRoot);
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT s.id, s.name, s.reference_video_json FROM shots s JOIN episodes e ON e.id = s.episode_id WHERE e.project_id = $projectId";
            command.Parameters.AddWithValue("$projectId", projectId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var source = new SourceContext(
                    projectId,
                    projectRoot,
                    reader.GetString(0),
                    ProjectTreeNodeKind.Shot,
                    ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Shot),
                    "Shot",
                    reader.GetString(1),
                    ReferenceUsageScope.Production,
                    ExternalMediaAuthoringSurface.Editor);
                var document = ShotReferenceVideoDocument.ParseRequired(
                    ReadString(reader, 2),
                    $"Shot '{source.NodeId}' reference video");
                AddPath(usages, source, "shot.referenceVideoPath", "shot.referenceVideoPath", "sourcePath", "Reference video", document.SourcePath, ValueKind.VideoFilePath, mediaRoot);
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT id, family_name, source_directory FROM production_fonts WHERE project_id = $projectId";
            command.Parameters.AddWithValue("$projectId", projectId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var source = new SourceContext(
                    projectId,
                    projectRoot,
                    reader.GetString(0),
                    ProjectTreeNodeKind.ProductionFont,
                    ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ProductionFont),
                    "Production Font",
                    reader.GetString(1),
                    ReferenceUsageScope.Production,
                    ExternalMediaAuthoringSurface.Editor);
                AddPath(
                    usages,
                    source,
                    "font.sourceDirectory",
                    "font.sourceDirectory",
                    "sourceDirectory",
                    "Family directory",
                    ReadString(reader, 2),
                    ValueKind.MediaDirectoryPath,
                    mediaRoot,
                    directoryKind: ExternalMediaDirectoryKind.ProductionFontFamily);
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT id, name, asset_root FROM icon_themes WHERE project_id = $projectId";
            command.Parameters.AddWithValue("$projectId", projectId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var source = new SourceContext(
                    projectId,
                    projectRoot,
                    reader.GetString(0),
                    ProjectTreeNodeKind.IconTheme,
                    ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.IconTheme),
                    "Icon Theme",
                    reader.GetString(1),
                    ReferenceUsageScope.Design,
                    ExternalMediaAuthoringSurface.Editor);
                AddPath(
                    usages,
                    source,
                    "iconTheme.assetRoot",
                    "iconTheme.assetRoot",
                    "assetRoot",
                    "Icon directory",
                    ReadString(reader, 2),
                    ValueKind.MediaDirectoryPath,
                    mediaRoot,
                    directoryKind: ExternalMediaDirectoryKind.IconTheme);
            }
        }
    }

    private static void AddComponentUsages(
        IReadOnlyList<ComponentOwner> components,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentIndex,
        string projectRoot,
        string mediaRoot,
        ICollection<ExternalMediaUsageDetail> usages)
    {
        foreach (var component in components)
        {
            foreach (var variant in component.Variants)
            {
                var source = new SourceContext(
                    component.ProjectId,
                    projectRoot,
                    variant.Reference,
                    ProjectTreeNodeKind.ComponentVariant,
                    ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ComponentVariant),
                    "Component Variant",
                    $"{component.Name} · {variant.Name}",
                    ReferenceUsageScope.Design,
                    ExternalMediaAuthoringSurface.Editor);
                ScanComponentConfig(
                    variant.Config,
                    component.RecordClassId,
                    source,
                    componentIndex,
                    mediaRoot,
                    usages,
                    [],
                    depth: 0,
                    focusOverride: null);

                AddRuntimeDocumentUsages(
                    component.DesignPreview,
                    variant.Config,
                    component.DesignPreview,
                    source with
                    {
                        AuthoringSurface = ExternalMediaAuthoringSurface.PreviewAuthoring,
                    },
                    componentIndex,
                    mediaRoot,
                    usages,
                    RuntimeValueSource.DesignPreview);
            }
        }
    }

    private static void AddModuleUsages(
        IReadOnlyList<ModuleOwner> modules,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentIndex,
        string projectRoot,
        string mediaRoot,
        ICollection<ExternalMediaUsageDetail> usages)
    {
        foreach (var module in modules)
        {
            foreach (var variant in module.Variants)
            {
                var source = new SourceContext(
                    module.ProjectId,
                    projectRoot,
                    variant.Reference,
                    ProjectTreeNodeKind.ModuleVariant,
                    ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleVariant),
                    "Module Variant",
                    $"{module.Name} · {variant.Name}",
                    ReferenceUsageScope.Design,
                    ExternalMediaAuthoringSurface.Editor);
                ScanModuleConfig(
                    variant.Config,
                    source,
                    componentIndex,
                    mediaRoot,
                    usages);
                AddRuntimeDocumentUsages(
                    module.DesignPreview,
                    variant.Config,
                    module.DesignPreview,
                    source with
                    {
                        AuthoringSurface = ExternalMediaAuthoringSurface.PreviewAuthoring,
                    },
                    componentIndex,
                    mediaRoot,
                    usages,
                    RuntimeValueSource.DesignPreview);
            }
        }
    }

    private static void AddScreenUsages(
        SqliteConnection connection,
        string projectId,
        IReadOnlyList<ModuleOwner> modules,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentIndex,
        string projectRoot,
        string mediaRoot,
        ICollection<ExternalMediaUsageDetail> usages)
    {
        var modulesById = modules.ToDictionary((module) => module.Id, StringComparer.Ordinal);
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT mi.id, mi.name, mi.module_id, mi.content_json, mi.metadata_json FROM module_instances mi JOIN apps a ON a.id = mi.app_id WHERE a.project_id = $projectId";
        command.Parameters.AddWithValue("$projectId", projectId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var instanceId = reader.GetString(0);
            var moduleId = reader.GetString(2);
            if (!modulesById.TryGetValue(moduleId, out var module))
            {
                throw new InvalidOperationException(
                    $"Screen '{instanceId}' references missing Module '{moduleId}'.");
            }
            var metadata = RequiredObject(reader, 4, $"Screen '{instanceId}' metadata_json");
            var variantReference = JsonPath.String(metadata, "moduleVariantReference", "");
            var variant = module.Variants.SingleOrDefault((candidate) =>
                    candidate.Reference.Equals(variantReference, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"Screen '{instanceId}' references missing Module Variant '{variantReference}'.");
            var source = new SourceContext(
                projectId,
                projectRoot,
                instanceId,
                ProjectTreeNodeKind.ModuleInstance,
                ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.ModuleInstance),
                "Screen",
                reader.GetString(1),
                ReferenceUsageScope.Production,
                ExternalMediaAuthoringSurface.PreviewAuthoring);
            AddRuntimeDocumentUsages(
                module.DesignPreview,
                variant.Config,
                RequiredObject(reader, 3, $"Screen '{instanceId}' content_json"),
                source,
                componentIndex,
                mediaRoot,
                usages,
                RuntimeValueSource.ProductionPayload);
        }
    }

    private static void ScanModuleConfig(
        JsonObject config,
        SourceContext source,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentIndex,
        string mediaRoot,
        ICollection<ExternalMediaUsageDetail> usages)
    {
        foreach (var descriptor in RecordClassFieldCatalog.All.Where((field) =>
                     field.Id.StartsWith("module.", StringComparison.Ordinal)
                     && field.ConfigJsonPath is { Length: > 0 }))
        {
            var value = JsonPath.Get(config, descriptor.ConfigJsonPath!);
            if (value is null) continue;
            AddDeclaredValue(
                descriptor.ValueKind,
                descriptor.Id,
                descriptor.Id,
                descriptor.ConfigJsonPath![^1],
                descriptor.Label,
                value,
                descriptor.ComponentInputBindings,
                descriptor.StructuredCollection,
                source,
                componentIndex,
                mediaRoot,
                usages,
                [],
                "");
        }

        foreach (var slot in EmbeddedComponentSlotCatalog.All().Where((candidate) =>
                     candidate.FieldId.StartsWith("module.", StringComparison.Ordinal)))
        {
            if (JsonPath.Get(config, slot.SlotPath) is not JsonObject slotNode
                || slotNode["overrides"] is not JsonObject overrides)
            {
                continue;
            }
            ScanComponentConfig(
                overrides,
                slot.RecordClassId,
                source,
                componentIndex,
                mediaRoot,
                usages,
                [slot.FieldId],
                depth: 1,
                focusOverride: null);
        }
    }

    private static void ScanComponentConfig(
        JsonObject config,
        string recordClassId,
        SourceContext source,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentIndex,
        string mediaRoot,
        ICollection<ExternalMediaUsageDetail> usages,
        IReadOnlyList<string> slotFieldIds,
        int depth,
        FocusOverride? focusOverride)
    {
        if (depth > 12)
        {
            throw new InvalidOperationException(
                $"External media ownership exceeds the supported declared recursion depth at '{source.NodeId}'.");
        }

        foreach (var descriptor in ComponentClassFieldCatalog.All())
        {
            if (descriptor.JsonPath.Length == 0
                || !descriptor.Id.StartsWith(
                    $"{recordClassId}.",
                    StringComparison.Ordinal)
                || descriptor.ValueKind == ValueKind.ComponentVariantSlot)
            {
                continue;
            }
            var value = JsonPath.Get(config, descriptor.JsonPath);
            if (value is null) continue;
            AddDeclaredValue(
                descriptor.ValueKind,
                focusOverride?.FieldId ?? descriptor.Id,
                descriptor.Id,
                descriptor.JsonPath[^1],
                descriptor.Label,
                value,
                descriptor.ComponentInputBindings,
                descriptor.StructuredCollection,
                source,
                componentIndex,
                mediaRoot,
                usages,
                focusOverride?.SlotFieldIds ?? slotFieldIds,
                focusOverride?.ItemId ?? "");
        }

        foreach (var slot in EmbeddedComponentSlotCatalog.All())
        {
            if (JsonPath.Get(config, slot.SlotPath) is not JsonObject slotNode
                || slotNode["overrides"] is not JsonObject overrides)
            {
                continue;
            }
            ScanComponentConfig(
                overrides,
                slot.RecordClassId,
                source,
                componentIndex,
                mediaRoot,
                usages,
                focusOverride?.SlotFieldIds
                    ?? [.. slotFieldIds, slot.FieldId],
                depth + 1,
                focusOverride);
        }
    }

    private static void AddRuntimeDocumentUsages(
        JsonObject preview,
        JsonObject config,
        JsonObject values,
        SourceContext source,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentIndex,
        string mediaRoot,
        ICollection<ExternalMediaUsageDetail> usages,
        RuntimeValueSource valueSource,
        FocusOverride? focusOverride = null)
    {
        foreach (var input in RuntimeInputDefinitionReader.ReadInputs(preview, config))
        {
            var value = RuntimeValue(values, input.JsonKey, valueSource);
            if (value is not null)
            {
                AddInputValue(
                    input,
                    value,
                    source,
                    componentIndex,
                    mediaRoot,
                    usages,
                    focusOverride,
                    isRuntimeDefault: false);
            }
            if (valueSource != RuntimeValueSource.DesignPreview) continue;
            var defaultValue = DefaultNode(input);
            if (defaultValue is null) continue;
            AddInputValue(
                input,
                defaultValue,
                source,
                componentIndex,
                mediaRoot,
                    usages,
                    focusOverride,
                    " · Default",
                    isRuntimeDefault: true);
        }

        foreach (var collection in RuntimeInputDefinitionReader.ReadCollections(preview, config))
        {
            var value = RuntimeCollectionValue(values, collection, valueSource);
            ScanCollection(
                value,
                collection,
                source,
                componentIndex,
                mediaRoot,
                usages,
                focusOverride);
        }
    }

    private static void AddInputValue(
        ComponentInputDefinition input,
        JsonNode value,
        SourceContext source,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentIndex,
        string mediaRoot,
        ICollection<ExternalMediaUsageDetail> usages,
        FocusOverride? focusOverride,
        string labelSuffix = "",
        bool isRuntimeDefault = false)
    {
        AddDeclaredValue(
            input.ValueKind,
            focusOverride?.FieldId ?? input.Id,
            input.Id,
            input.JsonKey,
            input.Label + labelSuffix,
            value,
            null,
            input.StructuredCollection,
            source,
            componentIndex,
            mediaRoot,
            usages,
            focusOverride?.SlotFieldIds ?? [],
            focusOverride?.ItemId ?? "",
            isRuntimeDefault);
    }

    private static void AddDeclaredValue(
        ValueKind valueKind,
        string fieldId,
        string declaredFieldId,
        string declaredJsonKey,
        string fieldLabel,
        JsonNode value,
        IReadOnlyList<ComponentInputBindingDefinition>? bindings,
        RuntimeInputCollectionDefinition? collection,
        SourceContext source,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentIndex,
        string mediaRoot,
        ICollection<ExternalMediaUsageDetail> usages,
        IReadOnlyList<string> slotFieldIds,
        string itemId,
        bool isRuntimeDefault = false)
    {
        if (MediaValueKinds.Contains(valueKind))
        {
            AddPath(
                usages,
                source,
                fieldId,
                declaredFieldId,
                declaredJsonKey,
                fieldLabel,
                StringValue(value),
                valueKind,
                mediaRoot,
                slotFieldIds,
                itemId,
                isRuntimeDefault);
            return;
        }
        if (valueKind == ValueKind.ComponentInputBindings
            && value is JsonObject bindingValues)
        {
            foreach (var binding in bindings ?? [])
            {
                var bindingValue = bindingValues[binding.JsonKey]
                    ?? DefaultNode(binding.ValueKind, binding.DefaultValue);
                if (bindingValue is null) continue;
                AddDeclaredValue(
                    binding.ValueKind,
                    fieldId,
                    binding.Id,
                    binding.JsonKey,
                    $"{fieldLabel} · {binding.Label}",
                    bindingValue,
                    null,
                    null,
                    source,
                    componentIndex,
                    mediaRoot,
                    usages,
                    slotFieldIds,
                    itemId,
                    isRuntimeDefault);
            }
            return;
        }
        if (valueKind == ValueKind.ComponentVariantSlot)
        {
            var slot = value as JsonObject
                ?? throw new InvalidOperationException(
                    $"External media field '{fieldLabel}' must be a Component Variant Slot object.");
            var reference = ComponentVariantSlotDocumentContract.VariantReference(
                slot,
                $"External media field '{fieldLabel}'");
            if (!componentIndex.TryGetValue(reference, out var component))
            {
                throw new InvalidOperationException(
                    $"External media field '{fieldLabel}' references missing Component Variant '{reference}'.");
            }
            ScanComponentConfig(
                ComponentVariantSlotDocumentContract.Overrides(
                    slot,
                    $"External media field '{fieldLabel}'"),
                component.Owner.RecordClassId,
                source,
                componentIndex,
                mediaRoot,
                usages,
                slotFieldIds,
                depth: 1,
                new FocusOverride(fieldId, itemId, slotFieldIds));
            return;
        }
        if (valueKind == ValueKind.StructuredCollection
            && collection is not null)
        {
            ScanCollection(
                value,
                collection,
                source,
                componentIndex,
                mediaRoot,
                usages,
                new FocusOverride(fieldId, itemId, slotFieldIds));
        }
    }

    private static void ScanCollection(
        JsonNode? value,
        RuntimeInputCollectionDefinition collection,
        SourceContext source,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentIndex,
        string mediaRoot,
        ICollection<ExternalMediaUsageDetail> usages,
        FocusOverride? parentFocus)
    {
        var items = value as JsonArray
            ?? throw new InvalidOperationException(
                $"External media collection '{collection.Label}' must be an array.");
        RuntimeCollectionDocumentContract.Validate(
            items,
            $"External media collection '{collection.Label}'");
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index] as JsonObject
                ?? throw new InvalidOperationException(
                    $"External media collection '{collection.Label}' item at index {index} must be an object.");
            var stableItemId = JsonPath.RequiredString(
                item,
                "id",
                $"External media collection '{collection.Label}' item at index {index}");
            var focus = new FocusOverride(
                parentFocus?.FieldId ?? collection.Id,
                stableItemId,
                parentFocus?.SlotFieldIds ?? []);
            foreach (var field in collection.Fields)
            {
                var fieldValue = item[field.JsonKey];
                if (fieldValue is null) continue;
                AddDeclaredValue(
                    field.ValueKind,
                    focus.FieldId,
                    field.Id,
                    field.JsonKey,
                    $"{collection.Label} · {stableItemId} · {field.Label}",
                    fieldValue,
                    null,
                    field.StructuredCollection,
                    source,
                    componentIndex,
                    mediaRoot,
                    usages,
                    focus.SlotFieldIds,
                    focus.ItemId);
            }

            if (collection.ComponentItems is not { } componentItems) continue;
            RuntimeComponentCollectionItemDocumentContract.ValidateItem(
                item,
                componentItems.DocumentKeys,
                $"External media collection '{collection.Label}' item '{stableItemId}'");
            var reference = RuntimeComponentCollectionItemDocumentContract.RequireVariantReference(
                item,
                componentItems.DocumentKeys,
                $"External media collection '{collection.Label}' item '{stableItemId}'");
            if (!componentIndex.TryGetValue(reference, out var component)) continue;
            ScanComponentConfig(
                RuntimeComponentCollectionItemDocumentContract.RequireOverrides(
                    item,
                    componentItems.DocumentKeys,
                    $"External media collection '{collection.Label}' item '{stableItemId}'"),
                component.Owner.RecordClassId,
                source,
                componentIndex,
                mediaRoot,
                usages,
                focus.SlotFieldIds,
                depth: 1,
                focus);
            AddRuntimeDocumentUsages(
                component.Owner.DesignPreview,
                component.Variant.Config,
                RuntimeComponentCollectionItemDocumentContract.RequireInputs(
                    item,
                    componentItems.DocumentKeys,
                    $"External media collection '{collection.Label}' item '{stableItemId}'"),
                source,
                componentIndex,
                mediaRoot,
                usages,
                RuntimeValueSource.ExplicitValues,
                focus);
        }
    }

    private static void AddPath(
        ICollection<ExternalMediaUsageDetail> usages,
        SourceContext source,
        string fieldId,
        string declaredFieldId,
        string declaredJsonKey,
        string fieldLabel,
        string authoredPath,
        ValueKind valueKind,
        string mediaRoot,
        IReadOnlyList<string>? slotFieldIds = null,
        string itemId = "",
        bool isRuntimeDefault = false,
        ExternalMediaDirectoryKind directoryKind = ExternalMediaDirectoryKind.None)
    {
        if (string.IsNullOrWhiteSpace(authoredPath)) return;
        var absolute = Resolve(source, authoredPath, valueKind, mediaRoot);
        var isDirectory = valueKind == ValueKind.MediaDirectoryPath;
        var directory = isDirectory
            ? absolute
            : Path.GetDirectoryName(absolute) ?? absolute;
        usages.Add(new ExternalMediaUsageDetail(
            source.ProjectId,
            source.NodeId,
            source.Kind,
            source.RecordClassId,
            source.TypeLabel,
            source.Name,
            source.Scope,
            source.AuthoringSurface,
            slotFieldIds?.ToArray() ?? [],
            fieldId,
            fieldLabel,
            itemId,
            authoredPath,
            valueKind,
            declaredFieldId,
            declaredJsonKey,
            isRuntimeDefault,
            isDirectory && directoryKind == ExternalMediaDirectoryKind.None
                ? ExternalMediaDirectoryKind.Media
                : directoryKind,
            absolute,
            directory,
            isDirectory
                ? directoryKind switch
                {
                    ExternalMediaDirectoryKind.ProductionFontFamily => "Font family folder",
                    ExternalMediaDirectoryKind.IconTheme => "Icon folder",
                    _ => "Media folder",
                }
                : Path.GetFileName(absolute),
            isDirectory,
            isDirectory ? Directory.Exists(absolute) : File.Exists(absolute)));
    }

    private static string Resolve(
        SourceContext source,
        string authoredPath,
        ValueKind valueKind,
        string mediaRoot)
    {
        var paths = new ProjectPathResolver(source.ProjectRoot);
        var resolved = valueKind == ValueKind.VideoFilePath
            ? paths.ResolveProjectPath(authoredPath)
            : paths.ResolveLocalPath(authoredPath, mediaRoot);
        return resolved
            ?? throw new InvalidOperationException(
                $"{source.TypeLabel} '{source.Name}' field path '{authoredPath}' cannot be resolved.");
    }

    private static JsonNode? RuntimeValue(
        JsonObject values,
        string jsonKey,
        RuntimeValueSource source) =>
        source == RuntimeValueSource.DesignPreview
        && values["testValues"] is JsonObject testValues
            ? testValues[jsonKey] ?? values[jsonKey]
            : values[jsonKey];

    private static JsonNode? RuntimeCollectionValue(
        JsonObject values,
        RuntimeInputCollectionDefinition collection,
        RuntimeValueSource source)
    {
        if (source == RuntimeValueSource.DesignPreview)
        {
            return new JsonArray(
                DesignPreviewTestValues.CollectionItems(values, collection)
                    .Select((item) => (JsonNode?)item.DeepClone())
                    .ToArray());
        }
        var jsonKey = source == RuntimeValueSource.ProductionPayload
                      && !string.IsNullOrWhiteSpace(collection.StorageCollectionJsonKey)
            ? collection.StorageCollectionJsonKey
            : collection.JsonKey;
        return values[jsonKey];
    }

    private static JsonNode? DefaultNode(ComponentInputDefinition input) =>
        DefaultNode(input.ValueKind, input.DefaultValue);

    private static JsonNode? DefaultNode(ValueKind valueKind, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (valueKind is ValueKind.StructuredCollection
            or ValueKind.ComponentInputBindings
            or ValueKind.ComponentVariantSlot)
        {
            return JsonNode.Parse(value);
        }
        return JsonValue.Create(value);
    }

    private static string StringValue(JsonNode value) =>
        value is JsonValue scalar
        && scalar.TryGetValue<string>(out var text)
            ? text
            : "";

    private static string ProjectMediaRoot(
        SqliteConnection connection,
        string projectId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT media_root FROM projects WHERE id = $projectId";
        command.Parameters.AddWithValue("$projectId", projectId);
        return command.ExecuteScalar() as string
            ?? throw new InvalidOperationException(
                $"External Media requires existing Project '{projectId}'.");
    }

    private static IReadOnlyList<ComponentOwner> ReadComponents(
        SqliteConnection connection,
        string projectId)
    {
        var result = new List<ComponentOwner>();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT id, component_type, name, record_class_id, design_preview_json, metadata_json FROM component_classes WHERE project_id = $projectId";
        command.Parameters.AddWithValue("$projectId", projectId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            result.Add(new ComponentOwner(
                id,
                projectId,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                RequiredObject(reader, 4, $"Component '{id}' design_preview_json"),
                ReadVariants(RequiredObject(reader, 5, $"Component '{id}' metadata_json"), id)));
        }
        return result;
    }

    private static IReadOnlyList<ModuleOwner> ReadModules(
        SqliteConnection connection,
        string projectId)
    {
        var result = new List<ModuleOwner>();
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT m.id, m.name, m.record_class_id, m.design_preview_json, m.metadata_json FROM modules m JOIN apps a ON a.id = m.app_id WHERE a.project_id = $projectId";
        command.Parameters.AddWithValue("$projectId", projectId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            result.Add(new ModuleOwner(
                id,
                projectId,
                reader.GetString(1),
                reader.GetString(2),
                RequiredObject(reader, 3, $"Module '{id}' design_preview_json"),
                ReadVariants(RequiredObject(reader, 4, $"Module '{id}' metadata_json"), id)));
        }
        return result;
    }

    private static IReadOnlyList<VariantOwner> ReadVariants(
        JsonObject metadata,
        string ownerId)
    {
        var variants = VariantEnvelopeContract.RequiredArray(
            metadata,
            "variants",
            $"Owner '{ownerId}'");
        return variants.Select((node) =>
        {
            var variant = node as JsonObject
                ?? throw new InvalidOperationException(
                    $"Variant on '{ownerId}' must be an object.");
            var id = JsonPath.RequiredString(variant, "id", $"Variant on '{ownerId}'");
            return new VariantOwner(
                id,
                JsonPath.RequiredString(variant, "name", $"Variant '{id}' on '{ownerId}'"),
                VariantReferenceId.Format(ownerId, id),
                variant["config"] as JsonObject
                    ?? throw new InvalidOperationException(
                        $"Variant '{id}' on '{ownerId}' has no config object."));
        }).ToArray();
    }

    private static JsonObject RequiredObject(
        SqliteDataReader reader,
        int ordinal,
        string owner) =>
        JsonPath.ParseRequiredObject(ReadString(reader, ordinal), owner);

    private static string ReadString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);

    private static string UsageIdentity(ExternalMediaUsageDetail usage) =>
        $"{usage.SourceNodeId}\u001f{usage.AuthoringSurface}\u001f{string.Join('/', usage.SlotFieldIds)}\u001f{usage.FieldId}\u001f{usage.DeclaredFieldId}\u001f{usage.ItemId}\u001f{usage.IsRuntimeDefault}\u001f{usage.AbsoluteTargetPath}";

    private sealed record SourceContext(
        string ProjectId,
        string ProjectRoot,
        string NodeId,
        ProjectTreeNodeKind Kind,
        string RecordClassId,
        string TypeLabel,
        string Name,
        ReferenceUsageScope Scope,
        ExternalMediaAuthoringSurface AuthoringSurface);

    private sealed record FocusOverride(
        string FieldId,
        string ItemId,
        IReadOnlyList<string> SlotFieldIds);

    private sealed record VariantOwner(
        string Id,
        string Name,
        string Reference,
        JsonObject Config);

    private sealed record ComponentOwner(
        string Id,
        string ProjectId,
        string ComponentType,
        string Name,
        string RecordClassId,
        JsonObject DesignPreview,
        IReadOnlyList<VariantOwner> Variants);

    private sealed record ComponentVariantOwner(
        ComponentOwner Owner,
        VariantOwner Variant);

    private sealed record ModuleOwner(
        string Id,
        string ProjectId,
        string Name,
        string RecordClassId,
        JsonObject DesignPreview,
        IReadOnlyList<VariantOwner> Variants);

    private enum RuntimeValueSource
    {
        DesignPreview,
        ProductionPayload,
        ExplicitValues,
    }
}
