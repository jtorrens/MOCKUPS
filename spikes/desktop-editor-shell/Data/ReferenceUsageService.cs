using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class ReferenceUsageService : IReferenceUsageService
{
    private static readonly (string Label, string[] Path)[] ModuleComponentReferencePaths =
    [
        ("Header avatar variant", ["conversation", "headerAvatarVariant"]),
        ("Text input variant", ["conversation", "textInputBarVariant"]),
        ("Keyboard variant", ["conversation", "keyboardVariant"]),
        ("Bubble variant", ["conversation", "bubbleVariant"]),
    ];

    private static readonly (string Label, string[] Path)[] ThemeFontReferencePaths =
    [
        ("Text font", ["typography", "fontFamilyId"]),
        ("System font", ["typography", "systemFontFamilyId"]),
        ("Emoji font", ["typography", "emojiFontFamilyId"]),
    ];

    private static readonly (string Label, string[] Path)[] ThemeShadowColorPaths =
    [
        ("Default shadow", ["shadows", "default", "color", "color"]),
        ("Elevated shadow", ["shadows", "elevated", "color", "color"]),
        ("Avatar shadow", ["shadows", "avatar", "color", "color"]),
    ];

    private static readonly (string Label, string[] Path)[] ActorPaletteReferencePaths =
    [
        ("Actor color · Light", ["modes", "light", "color"]),
        ("Actor color · Dark", ["modes", "dark", "color"]),
        ("Avatar text · Light", ["modes", "light", "avatarTextColor"]),
        ("Avatar text · Dark", ["modes", "dark", "avatarTextColor"]),
        ("Wallpaper · Light", ["modes", "light", "wallpaper", "color"]),
        ("Wallpaper · Dark", ["modes", "dark", "wallpaper", "color"]),
    ];

    private static readonly (string Label, string[] Path)[] AppPaletteReferencePaths =
    [
        ("Wallpaper · Light", ["modes", "light", "wallpaper", "color"]),
        ("Wallpaper · Dark", ["modes", "dark", "wallpaper", "color"]),
    ];

    private static readonly IReadOnlyDictionary<string, ProjectTreeNodeKind> RecordReferenceKinds =
        new Dictionary<string, ProjectTreeNodeKind>(StringComparer.Ordinal)
        {
            ["actors"] = ProjectTreeNodeKind.Actor,
            ["devices"] = ProjectTreeNodeKind.Device,
            ["themes"] = ProjectTreeNodeKind.Theme,
            ["production_fonts"] = ProjectTreeNodeKind.ProductionFont,
            ["icon_themes"] = ProjectTreeNodeKind.IconTheme,
            ["render_presets"] = ProjectTreeNodeKind.RenderPreset,
        };

    private readonly SqliteProjectContext _context;

    public ReferenceUsageService(SqliteProjectContext context)
    {
        _context = context;
    }

    public IReadOnlyDictionary<ReferenceTarget, IReadOnlyList<ReferenceUsageRecord>> BuildIndex(
        SqliteConnection connection)
    {
        return ReadAll(connection)
            .GroupBy((usage) => usage.Referenced)
            .ToDictionary(
                (group) => group.Key,
                (group) => (IReadOnlyList<ReferenceUsageRecord>)group.ToList());
    }

    public IReadOnlyList<ReferenceUsageRecord> GetUsages(ProjectTreeNodeKind targetKind, string targetId)
    {
        using var connection = _context.OpenConnection();
        return GetUsages(connection, targetKind, targetId);
    }

    public IReadOnlyList<ReferenceUsageRecord> GetUsages(
        SqliteConnection connection,
        ProjectTreeNodeKind targetKind,
        string targetId)
    {
        var target = new ReferenceTarget(targetKind, targetId);
        return ReadAll(connection)
            .Where((usage) => usage.Referenced == target)
            .ToList();
    }

    private static IReadOnlyList<ReferenceUsageRecord> ReadAll(SqliteConnection connection)
    {
        var components = ReadComponents(connection);
        var modules = ReadModules(connection);
        var targets = ReadTargets(connection, components, modules);
        var usages = new List<ReferenceUsageRecord>();

        AddRelationalReferences(connection, targets, usages);
        AddThemeReferences(connection, targets, usages);
        AddActorReferences(connection, targets, usages);
        AddAppReferences(connection, targets, usages);
        AddComponentReferences(components, targets, usages);
        AddModuleReferences(modules, components, targets, usages);
        AddModuleInstanceRuntimeReferences(connection, modules, components, targets, usages);

        return usages
            .GroupBy(UsageIdentity, StringComparer.Ordinal)
            .Select((group) => group.First())
            .OrderBy((usage) => usage.Scope)
            .ThenBy((usage) => usage.SourceTypeLabel, StringComparer.OrdinalIgnoreCase)
            .ThenBy((usage) => usage.SourceName, StringComparer.OrdinalIgnoreCase)
            .ThenBy((usage) => usage.FieldLabel, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static TargetCatalog ReadTargets(
        SqliteConnection connection,
        IReadOnlyList<ComponentOwner> components,
        IReadOnlyList<ModuleOwner> modules)
    {
        var catalog = new TargetCatalog();
        ReadTargetRows(connection, catalog, ProjectTreeNodeKind.Episode, "SELECT id FROM episodes");
        ReadTargetRows(connection, catalog, ProjectTreeNodeKind.Shot, "SELECT id FROM shots");
        ReadTargetRows(connection, catalog, ProjectTreeNodeKind.App, "SELECT id FROM apps");
        ReadTargetRows(connection, catalog, ProjectTreeNodeKind.Module, "SELECT id FROM modules");
        ReadTargetRows(connection, catalog, ProjectTreeNodeKind.ModuleInstance, "SELECT id FROM module_instances");
        ReadTargetRows(connection, catalog, ProjectTreeNodeKind.Device, "SELECT id FROM devices");
        ReadTargetRows(connection, catalog, ProjectTreeNodeKind.Actor, "SELECT id FROM actors");
        ReadTargetRows(connection, catalog, ProjectTreeNodeKind.Theme, "SELECT id FROM themes");
        ReadTargetRows(connection, catalog, ProjectTreeNodeKind.ProductionFont, "SELECT id FROM production_fonts");
        ReadTargetRows(connection, catalog, ProjectTreeNodeKind.IconTheme, "SELECT id FROM icon_themes");
        ReadTargetRows(connection, catalog, ProjectTreeNodeKind.RenderPreset, "SELECT id FROM render_presets");

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, project_id, token FROM palette_colors";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                catalog.Add(ProjectTreeNodeKind.PaletteColor, reader.GetString(2), reader.GetString(0), reader.GetString(1));
            }
        }

        foreach (var component in components)
        {
            foreach (var variant in component.Variants)
            {
                catalog.Add(ProjectTreeNodeKind.ComponentVariant, variant.Reference, variant.Reference);
            }
        }

        foreach (var module in modules)
        {
            foreach (var variant in module.Variants)
            {
                catalog.Add(ProjectTreeNodeKind.ModuleVariant, variant.Reference, variant.Reference);
            }
        }

        return catalog;
    }

    private static void ReadTargetRows(
        SqliteConnection connection,
        TargetCatalog catalog,
        ProjectTreeNodeKind kind,
        string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            catalog.Add(kind, id, id);
        }
    }

    private static void AddRelationalReferences(
        SqliteConnection connection,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT s.id, s.name, s.episode_id, s.owner_actor_id, s.render_preset_id, e.project_id FROM shots s JOIN episodes e ON e.id = s.episode_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var source = new SourceContext(reader.GetString(0), ProjectTreeNodeKind.Shot, "Shot", reader.GetString(1), ReferenceUsageScope.Production, reader.GetString(5));
                AddExact(usages, targets, ProjectTreeNodeKind.Episode, reader.GetString(2), source, "Episode");
                AddExact(usages, targets, ProjectTreeNodeKind.Actor, ReadString(reader, 3), source, "Owner actor");
                AddExact(usages, targets, ProjectTreeNodeKind.RenderPreset, ReadString(reader, 4), source, "Render preset");
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT mi.id, mi.name, mi.shot_id, mi.app_id, mi.module_id, mi.metadata_json, a.project_id FROM module_instances mi JOIN apps a ON a.id = mi.app_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var source = new SourceContext(reader.GetString(0), ProjectTreeNodeKind.ModuleInstance, "Screen", reader.GetString(1), ReferenceUsageScope.Production, reader.GetString(6));
                AddExact(usages, targets, ProjectTreeNodeKind.Shot, reader.GetString(2), source, "Shot");
                AddExact(usages, targets, ProjectTreeNodeKind.App, reader.GetString(3), source, "App");
                AddExact(usages, targets, ProjectTreeNodeKind.Module, reader.GetString(4), source, "Module");
                var metadata = JsonPath.ParseRequiredObject(ReadString(reader, 5), $"Module Instance '{source.NodeId}' metadata_json");
                AddExact(
                    usages,
                    targets,
                    ProjectTreeNodeKind.ModuleVariant,
                    JsonPath.String(metadata, "moduleVariantReference", ""),
                    source,
                    "Module variant");
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT m.id, m.name, m.app_id, a.project_id FROM modules m JOIN apps a ON a.id = m.app_id";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var source = new SourceContext(reader.GetString(0), ProjectTreeNodeKind.Module, "Module", reader.GetString(1), ReferenceUsageScope.Design, reader.GetString(3));
                AddExact(usages, targets, ProjectTreeNodeKind.App, reader.GetString(2), source, "App");
            }
        }

        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT id, display_name, default_device_id, default_theme_id, project_id FROM actors";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var source = new SourceContext(reader.GetString(0), ProjectTreeNodeKind.Actor, "Actor", reader.GetString(1), ReferenceUsageScope.Production, reader.GetString(4));
                AddExact(usages, targets, ProjectTreeNodeKind.Device, ReadString(reader, 2), source, "Default device");
                AddExact(usages, targets, ProjectTreeNodeKind.Theme, ReadString(reader, 3), source, "Default theme");
            }
        }
    }

    private static void AddThemeReferences(
        SqliteConnection connection,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, icon_theme_id, status_bar_id, navigation_bar_id, tokens_json, project_id FROM themes";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var source = new SourceContext(reader.GetString(0), ProjectTreeNodeKind.Theme, "Theme", reader.GetString(1), ReferenceUsageScope.Design, reader.GetString(6));
            AddExact(usages, targets, ProjectTreeNodeKind.IconTheme, ReadString(reader, 2), source, "Icon theme");
            AddExact(usages, targets, ProjectTreeNodeKind.ComponentVariant, ReadString(reader, 3), source, "Status bar");
            AddExact(usages, targets, ProjectTreeNodeKind.ComponentVariant, ReadString(reader, 4), source, "Navigation bar");

            var tokens = JsonPath.ParseRequiredObject(ReadString(reader, 5), $"Theme '{source.NodeId}' tokens_json");
            foreach (var token in ThemeColorTokenCatalog.ColorTokens)
            {
                AddJsonString(usages, targets, ProjectTreeNodeKind.PaletteColor, tokens, token.LightPath, source, $"{token.Id} · Light");
                AddJsonString(usages, targets, ProjectTreeNodeKind.PaletteColor, tokens, token.DarkPath, source, $"{token.Id} · Dark");
            }
            foreach (var declaration in ThemeShadowColorPaths)
            {
                AddJsonString(usages, targets, ProjectTreeNodeKind.PaletteColor, tokens, declaration.Path, source, declaration.Label);
            }
            foreach (var declaration in ThemeFontReferencePaths)
            {
                AddJsonString(usages, targets, ProjectTreeNodeKind.ProductionFont, tokens, declaration.Path, source, declaration.Label);
            }
        }
    }

    private static void AddActorReferences(
        SqliteConnection connection,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, display_name, metadata_json, project_id FROM actors";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var source = new SourceContext(reader.GetString(0), ProjectTreeNodeKind.Actor, "Actor", reader.GetString(1), ReferenceUsageScope.Production, reader.GetString(3));
            var metadata = JsonPath.ParseRequiredObject(ReadString(reader, 2), $"Actor '{source.NodeId}' metadata_json");
            foreach (var declaration in ActorPaletteReferencePaths)
            {
                AddJsonString(usages, targets, ProjectTreeNodeKind.PaletteColor, metadata, declaration.Path, source, declaration.Label);
            }
        }
    }

    private static void AddAppReferences(
        SqliteConnection connection,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, name, config_json, project_id FROM apps";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var source = new SourceContext(reader.GetString(0), ProjectTreeNodeKind.App, "App", reader.GetString(1), ReferenceUsageScope.Design, reader.GetString(3));
            var config = JsonPath.ParseRequiredObject(ReadString(reader, 2), $"App '{source.NodeId}' config_json");
            foreach (var declaration in AppPaletteReferencePaths)
            {
                AddJsonString(usages, targets, ProjectTreeNodeKind.PaletteColor, config, declaration.Path, source, declaration.Label);
            }
        }
    }

    private static void AddComponentReferences(
        IReadOnlyList<ComponentOwner> components,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages)
    {
        var componentsByReference = ComponentReferenceIndex(components);
        foreach (var component in components)
        {
            foreach (var variant in component.Variants)
            {
                var source = new SourceContext(
                    variant.Reference,
                    ProjectTreeNodeKind.ComponentVariant,
                    "Component Variant",
                    $"{component.Name} · {variant.Name}",
                    ReferenceUsageScope.Design,
                    component.ProjectId,
                    component);
                ScanComponentConfig(variant.Config, source, targets, usages, componentsByReference, depth: 0);
            }

            var defaultVariant = component.Variants.Single((variant) => variant.Id.Equals("default", StringComparison.Ordinal));
            var previewSource = new SourceContext(
                component.Id,
                ProjectTreeNodeKind.ComponentClass,
                "Component Class",
                component.Name,
                ReferenceUsageScope.Design,
                component.ProjectId,
                component);
            AddRuntimeDocumentReferences(
                component.DesignPreview,
                defaultVariant.Config,
                component.DesignPreview,
                previewSource,
                targets,
                usages,
                componentsByReference,
                RuntimeValueSource.DesignPreview);
        }
    }

    private static void AddModuleReferences(
        IReadOnlyList<ModuleOwner> modules,
        IReadOnlyList<ComponentOwner> components,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages)
    {
        var componentsByReference = ComponentReferenceIndex(components);
        foreach (var module in modules)
        {
            foreach (var variant in module.Variants)
            {
                var source = new SourceContext(
                    variant.Reference,
                    ProjectTreeNodeKind.ModuleVariant,
                    "Module Variant",
                    $"{module.Name} · {variant.Name}",
                    ReferenceUsageScope.Design,
                    module.ProjectId);
                ScanModuleConfig(variant.Config, source, targets, usages, componentsByReference);
            }

            var defaultVariant = module.Variants.Single((variant) => variant.Id.Equals("default", StringComparison.Ordinal));
            var previewSource = new SourceContext(module.Id, ProjectTreeNodeKind.Module, "Module", module.Name, ReferenceUsageScope.Design, module.ProjectId);
            AddRuntimeDocumentReferences(
                module.DesignPreview,
                defaultVariant.Config,
                module.DesignPreview,
                previewSource,
                targets,
                usages,
                componentsByReference,
                RuntimeValueSource.DesignPreview);
        }
    }

    private static void AddModuleInstanceRuntimeReferences(
        SqliteConnection connection,
        IReadOnlyList<ModuleOwner> modules,
        IReadOnlyList<ComponentOwner> components,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages)
    {
        var modulesById = modules.ToDictionary((module) => module.Id, StringComparer.Ordinal);
        var componentsByReference = ComponentReferenceIndex(components);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT mi.id, mi.name, mi.module_id, mi.content_json, mi.metadata_json, a.project_id FROM module_instances mi JOIN apps a ON a.id = mi.app_id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var instanceId = reader.GetString(0);
            var moduleId = reader.GetString(2);
            if (!modulesById.TryGetValue(moduleId, out var module))
            {
                throw new InvalidOperationException($"Module Instance '{instanceId}' references missing Module '{moduleId}'.");
            }

            var metadata = JsonPath.ParseRequiredObject(ReadString(reader, 4), $"Module Instance '{instanceId}' metadata_json");
            var variantReference = JsonPath.String(metadata, "moduleVariantReference", "");
            var variant = module.Variants.SingleOrDefault((candidate) => candidate.Reference.Equals(variantReference, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"Module Instance '{instanceId}' references missing Module Variant '{variantReference}'.");
            var content = JsonPath.ParseRequiredObject(ReadString(reader, 3), $"Module Instance '{instanceId}' content_json");
            var source = new SourceContext(instanceId, ProjectTreeNodeKind.ModuleInstance, "Screen", reader.GetString(1), ReferenceUsageScope.Production, reader.GetString(5));
            AddRuntimeDocumentReferences(
                module.DesignPreview,
                variant.Config,
                content,
                source,
                targets,
                usages,
                componentsByReference,
                RuntimeValueSource.ProductionPayload);
        }
    }

    private static void ScanModuleConfig(
        JsonObject config,
        SourceContext source,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentsByReference)
    {
        foreach (var declaration in ModuleComponentReferencePaths)
        {
            AddJsonString(usages, targets, ProjectTreeNodeKind.ComponentVariant, config, declaration.Path, source, declaration.Label);
        }

        foreach (var slot in EmbeddedComponentSlotCatalog.All().Where((candidate) => candidate.FieldId.StartsWith("module.", StringComparison.Ordinal)))
        {
            if (JsonPath.Get(config, slot.SlotPath) is not JsonObject slotNode)
            {
                continue;
            }
            AddExact(
                usages,
                targets,
                ProjectTreeNodeKind.ComponentVariant,
                JsonPath.String(slotNode, "variantReference", ""),
                source,
                slot.Label);
        }

        ScanBindings(
            JsonPath.Get(config, ["conversation", "headerLeftIconRowInputs"]),
            ComponentClassFieldCatalog.VariantInputBindingsForComponent("iconRow"),
            "Left icon row",
            source,
            targets,
            usages,
            componentsByReference);
        ScanBindings(
            JsonPath.Get(config, ["conversation", "headerRightIconRowInputs"]),
            ComponentClassFieldCatalog.VariantInputBindingsForComponent("iconRow"),
            "Right icon row",
            source,
            targets,
            usages,
            componentsByReference);

        var stackReference = "";
        var stackSlot = JsonPath.Get(config, ["lockScreen", "stackSlot"]) as JsonObject;
        stackReference = stackSlot is null ? stackReference : JsonPath.String(stackSlot, "variantReference", "");
        if (componentsByReference.TryGetValue(stackReference, out var stack)
            && JsonPath.Get(config, ["lockScreen", "stackInputs"]) is JsonObject stackInputs)
        {
            AddRuntimeDocumentReferences(
                stack.Owner.DesignPreview,
                stack.Variant.Config,
                stackInputs,
                source,
                targets,
                usages,
                componentsByReference,
                RuntimeValueSource.ExplicitValues);
        }
    }

    private static void ScanComponentConfig(
        JsonObject config,
        SourceContext source,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentsByReference,
        int depth)
    {
        if (depth > 12)
        {
            throw new InvalidOperationException($"Component reference ownership exceeds the supported declared recursion depth at '{source.NodeId}'.");
        }

        foreach (var descriptor in ComponentClassFieldCatalog.All())
        {
            if (descriptor.JsonPath.Length == 0) continue;
            var value = JsonPath.Get(config, descriptor.JsonPath);
            if (value is null) continue;

            ReferenceEmbeddedContext? embedded = null;
            if (depth == 0
                && descriptor.ValueKind == ValueKind.ComponentVariant
                && source.ComponentOwner is not null
                && EmbeddedComponentSlotCatalog.TryGet(descriptor.Id, out var embeddedSlot)
                && JsonPath.Get(config, embeddedSlot.SlotPath) is JsonObject embeddedNode)
            {
                embedded = new ReferenceEmbeddedContext(
                    source.ComponentOwner.Id,
                    source.ComponentOwner.Name,
                    source.ComponentOwner.ComponentType,
                    embeddedSlot.FieldId,
                    embeddedSlot.Label,
                    embeddedNode["overrides"] is JsonObject overrides && overrides.Count > 0,
                    source.NodeId);
            }

            AddDescriptorValue(
                descriptor,
                value,
                source,
                descriptor.Label,
                targets,
                usages,
                componentsByReference,
                embedded);
        }

        foreach (var slot in EmbeddedComponentSlotCatalog.All())
        {
            if (JsonPath.Get(config, slot.SlotPath) is not JsonObject slotNode
                || slotNode["overrides"] is not JsonObject overrides)
            {
                continue;
            }
            ScanComponentConfig(overrides, source, targets, usages, componentsByReference, depth + 1);
        }
    }

    private static void AddDescriptorValue(
        ComponentClassFieldDescriptor descriptor,
        JsonNode value,
        SourceContext source,
        string fieldLabel,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentsByReference,
        ReferenceEmbeddedContext? embedded)
    {
        if (descriptor.ValueKind == ValueKind.ComponentVariant)
        {
            AddExact(usages, targets, ProjectTreeNodeKind.ComponentVariant, StringValue(value), source, fieldLabel, embedded);
            return;
        }
        if (descriptor.ValueKind == ValueKind.ComponentInputBindings)
        {
            ScanBindings(value, descriptor.ComponentInputBindings ?? [], fieldLabel, source, targets, usages, componentsByReference);
            return;
        }
        if (descriptor.ValueKind == ValueKind.StructuredCollection && descriptor.StructuredCollection is not null)
        {
            ScanCollection(value, descriptor.StructuredCollection, fieldLabel, source, targets, usages, componentsByReference);
            return;
        }
        AddTypedValue(descriptor.ValueKind, "", value, source, fieldLabel, targets, usages);
    }

    private static void AddRuntimeDocumentReferences(
        JsonObject preview,
        JsonObject config,
        JsonObject values,
        SourceContext source,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentsByReference,
        RuntimeValueSource valueSource)
    {
        var includeContractDefaults = valueSource == RuntimeValueSource.DesignPreview;
        foreach (var input in ComponentPreviewInputSession.ReadRuntimeInputs(preview, config))
        {
            AddInputReference(
                input,
                RuntimeValue(values, input.JsonKey, valueSource),
                source,
                input.Label,
                targets,
                usages,
                componentsByReference);
            if (includeContractDefaults)
            {
                AddInputReference(
                    input,
                    DefaultNode(input),
                    source,
                    $"{input.Label} · Default",
                    targets,
                    usages,
                    componentsByReference);
            }
        }

        foreach (var collection in ComponentPreviewInputSession.ReadRuntimeCollections(preview, config))
        {
            var collectionValue = RuntimeCollectionValue(values, collection, valueSource);
            ScanCollection(collectionValue, collection, collection.Label, source, targets, usages, componentsByReference);
            if (!includeContractDefaults) continue;
            foreach (var field in collection.Fields)
            {
                AddInputReference(
                    field,
                    DefaultNode(field),
                    source,
                    $"{collection.Label} · {field.Label} · Default",
                    targets,
                    usages,
                    componentsByReference);
            }
        }
    }

    private static JsonNode? RuntimeValue(
        JsonObject values,
        string jsonKey,
        RuntimeValueSource valueSource)
    {
        return valueSource == RuntimeValueSource.DesignPreview
               && values["testValues"] is JsonObject testValues
            ? testValues[jsonKey] ?? values[jsonKey]
            : values[jsonKey];
    }

    private static JsonNode? RuntimeCollectionValue(
        JsonObject values,
        RuntimeInputCollectionDefinition collection,
        RuntimeValueSource valueSource)
    {
        if (valueSource == RuntimeValueSource.DesignPreview)
        {
            return new JsonArray(DesignPreviewTestValues.CollectionItems(values, collection)
                .Select((item) => (JsonNode?)item.DeepClone())
                .ToArray());
        }
        var jsonKey = valueSource == RuntimeValueSource.ProductionPayload
                      && !string.IsNullOrWhiteSpace(collection.StorageCollectionJsonKey)
            ? collection.StorageCollectionJsonKey
            : collection.JsonKey;
        return values[jsonKey];
    }

    private static void AddInputReference(
        ComponentInputDefinition input,
        JsonNode? value,
        SourceContext source,
        string fieldLabel,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentsByReference)
    {
        if (value is null) return;
        if (input.ValueKind == ValueKind.StructuredCollection && input.StructuredCollection is not null)
        {
            ScanCollection(value, input.StructuredCollection, fieldLabel, source, targets, usages, componentsByReference);
            return;
        }
        AddTypedValue(input.ValueKind, input.TableId, value, source, fieldLabel, targets, usages);
    }

    private static void ScanCollection(
        JsonNode? value,
        RuntimeInputCollectionDefinition collection,
        string fieldLabel,
        SourceContext source,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentsByReference)
    {
        var items = value as JsonArray;
        if (items is null) return;
        foreach (var item in items.OfType<JsonObject>())
        {
            var stableItemId = JsonPath.String(item, "id", "");
            var itemLabel = string.IsNullOrWhiteSpace(stableItemId) ? fieldLabel : $"{fieldLabel} · {stableItemId}";
            foreach (var field in collection.Fields)
            {
                AddInputReference(field, item[field.JsonKey], source, $"{itemLabel} · {field.Label}", targets, usages, componentsByReference);
            }

            if (collection.ComponentItems is not { } componentItems) continue;
            var reference = JsonPath.String(item, componentItems.VariantReferenceJsonKey, "");
            AddExact(usages, targets, ProjectTreeNodeKind.ComponentVariant, reference, source, $"{itemLabel} · Component variant");
            if (!componentsByReference.TryGetValue(reference, out var component)) continue;
            if (item[componentItems.OverridesJsonKey] is JsonObject overrides)
            {
                ScanComponentConfig(overrides, source, targets, usages, componentsByReference, depth: 1);
            }
            if (item[componentItems.InputsJsonKey] is JsonObject inputs)
            {
                AddRuntimeDocumentReferences(
                    component.Owner.DesignPreview,
                    component.Variant.Config,
                    inputs,
                    source,
                    targets,
                    usages,
                    componentsByReference,
                    RuntimeValueSource.ExplicitValues);
            }
        }
    }

    private static void ScanBindings(
        JsonNode? value,
        IReadOnlyList<ComponentInputBindingDefinition> bindings,
        string fieldLabel,
        SourceContext source,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages,
        IReadOnlyDictionary<string, ComponentVariantOwner> componentsByReference)
    {
        if (value is not JsonObject values) return;
        foreach (var binding in bindings)
        {
            var bindingValue = values[binding.JsonKey] ?? DefaultNode(binding.ValueKind, binding.DefaultValue);
            if (bindingValue is null) continue;
            AddTypedValue(binding.ValueKind, binding.TableId, bindingValue, source, $"{fieldLabel} · {binding.Label}", targets, usages);
        }
    }

    private static void AddTypedValue(
        ValueKind valueKind,
        string tableId,
        JsonNode value,
        SourceContext source,
        string fieldLabel,
        TargetCatalog targets,
        ICollection<ReferenceUsageRecord> usages)
    {
        switch (valueKind)
        {
            case ValueKind.RecordReference when RecordReferenceKinds.TryGetValue(tableId, out var targetKind):
                AddExact(usages, targets, targetKind, StringValue(value), source, fieldLabel);
                return;
            case ValueKind.ComponentVariant:
                AddExact(usages, targets, ProjectTreeNodeKind.ComponentVariant, StringValue(value), source, fieldLabel);
                return;
            case ValueKind.PaletteColorToken:
                AddExact(usages, targets, ProjectTreeNodeKind.PaletteColor, StringValue(value), source, fieldLabel);
                return;
            case ValueKind.PaletteColorPair:
            case ValueKind.PaletteColorAlphaPair:
                foreach (var token in StringValue(value).Split('|', StringSplitOptions.RemoveEmptyEntries).Take(2))
                {
                    AddExact(usages, targets, ProjectTreeNodeKind.PaletteColor, token, source, fieldLabel);
                }
                return;
            case ValueKind.TypographyStyle:
            case ValueKind.TypographySystemStyle:
                var typography = ObjectValue(value);
                AddExact(
                    usages,
                    targets,
                    ProjectTreeNodeKind.ProductionFont,
                    typography is null ? "" : JsonPath.String(typography, TypographyStyleValue.FontFamilyId, ""),
                    source,
                    fieldLabel);
                return;
        }
    }

    private static JsonNode? DefaultNode(ComponentInputDefinition input) =>
        DefaultNode(input.ValueKind, input.DefaultValue);

    private static JsonNode? DefaultNode(ValueKind valueKind, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (valueKind is ValueKind.StructuredCollection or ValueKind.ComponentInputBindings
            or ValueKind.TypographyStyle or ValueKind.TypographySystemStyle)
        {
            return JsonNode.Parse(value);
        }
        return JsonValue.Create(value);
    }

    private static JsonObject? ObjectValue(JsonNode value)
    {
        if (value is JsonObject objectValue) return objectValue;
        var text = StringValue(value);
        return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text) as JsonObject;
    }

    private static string StringValue(JsonNode value) =>
        value is JsonValue scalar && scalar.TryGetValue<string>(out var text) ? text : "";

    private static void AddJsonString(
        ICollection<ReferenceUsageRecord> usages,
        TargetCatalog targets,
        ProjectTreeNodeKind targetKind,
        JsonObject owner,
        IReadOnlyList<string> path,
        SourceContext source,
        string fieldLabel)
    {
        AddExact(usages, targets, targetKind, JsonPath.String(owner, path.ToArray()), source, fieldLabel);
    }

    private static void AddExact(
        ICollection<ReferenceUsageRecord> usages,
        TargetCatalog targets,
        ProjectTreeNodeKind targetKind,
        string serializedValue,
        SourceContext source,
        string fieldLabel,
        ReferenceEmbeddedContext? embeddedContext = null)
    {
        if (string.IsNullOrWhiteSpace(serializedValue)
            || !targets.TryResolve(targetKind, serializedValue, source.ProjectId, out var target))
        {
            return;
        }
        if (target.Kind == source.Kind && target.Id.Equals(source.NodeId, StringComparison.Ordinal)) return;
        usages.Add(new ReferenceUsageRecord(
            target,
            source.NodeId,
            source.Kind,
            source.TypeLabel,
            source.Name,
            fieldLabel,
            source.Scope,
            embeddedContext));
    }

    private static IReadOnlyDictionary<string, ComponentVariantOwner> ComponentReferenceIndex(
        IReadOnlyList<ComponentOwner> components)
    {
        return components
            .SelectMany((component) => component.Variants.Select((variant) => new ComponentVariantOwner(component, variant)))
            .ToDictionary((entry) => entry.Variant.Reference, StringComparer.Ordinal);
    }

    private static IReadOnlyList<ComponentOwner> ReadComponents(SqliteConnection connection)
    {
        var components = new List<ComponentOwner>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, project_id, component_type, name, design_preview_json, metadata_json FROM component_classes";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var metadata = JsonPath.ParseRequiredObject(ReadString(reader, 5), $"Component class '{id}' metadata_json");
            var variants = ReadVariants(metadata, "variants", id, "variant");
            components.Add(new ComponentOwner(
                id,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                JsonPath.ParseRequiredObject(ReadString(reader, 4), $"Component class '{id}' design_preview_json"),
                variants));
        }
        return components;
    }

    private static IReadOnlyList<ModuleOwner> ReadModules(SqliteConnection connection)
    {
        var modules = new List<ModuleOwner>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT m.id, a.project_id, m.name, m.design_preview_json, m.metadata_json FROM modules m JOIN apps a ON a.id = m.app_id";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var metadata = JsonPath.ParseRequiredObject(ReadString(reader, 4), $"Module '{id}' metadata_json");
            var variants = ReadVariants(metadata, "variants", id, "variant");
            modules.Add(new ModuleOwner(
                id,
                reader.GetString(1),
                reader.GetString(2),
                JsonPath.ParseRequiredObject(ReadString(reader, 3), $"Module '{id}' design_preview_json"),
                variants));
        }
        return modules;
    }

    private static IReadOnlyList<VariantOwner> ReadVariants(
        JsonObject metadata,
        string key,
        string ownerId,
        string referenceSegment)
    {
        var variants = VariantEnvelopeContract.RequiredArray(metadata, key, $"Owner '{ownerId}'");
        var result = new List<VariantOwner>(variants.Count);
        foreach (var item in variants)
        {
            var variant = item as JsonObject
                ?? throw new InvalidOperationException($"Variant on '{ownerId}' must be an object.");
            var id = JsonPath.String(variant, "id", "");
            result.Add(new VariantOwner(
                id,
                JsonPath.String(variant, "name", ""),
                $"{ownerId}::{referenceSegment}::{id}",
                variant["config"] as JsonObject
                    ?? throw new InvalidOperationException($"Variant '{id}' on '{ownerId}' has no config object.")));
        }
        return result;
    }

    private static string UsageIdentity(ReferenceUsageRecord usage) =>
        $"{usage.Referenced.Kind}\u001f{usage.Referenced.Id}\u001f{usage.SourceKind}\u001f{usage.SourceNodeId}\u001f{usage.FieldLabel}\u001f{usage.Scope}";

    private static string ReadString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? "" : reader.GetString(ordinal);

    private sealed class TargetCatalog
    {
        private readonly Dictionary<(ProjectTreeNodeKind Kind, string ProjectId), Dictionary<string, string>> _targets = [];

        public void Add(ProjectTreeNodeKind kind, string serializedValue, string stableId, string projectId = "")
        {
            var key = (kind, projectId);
            if (!_targets.TryGetValue(key, out var values))
            {
                values = new Dictionary<string, string>(StringComparer.Ordinal);
                _targets[key] = values;
            }
            values.Add(serializedValue, stableId);
        }

        public bool TryResolve(ProjectTreeNodeKind kind, string serializedValue, string projectId, out ReferenceTarget target)
        {
            if (TryResolve((kind, projectId), serializedValue, out target)
                || TryResolve((kind, ""), serializedValue, out target))
            {
                return true;
            }
            target = new ReferenceTarget(kind, "");
            return false;
        }

        private bool TryResolve(
            (ProjectTreeNodeKind Kind, string ProjectId) key,
            string serializedValue,
            out ReferenceTarget target)
        {
            if (_targets.TryGetValue(key, out var values)
                && values.TryGetValue(serializedValue, out var stableId))
            {
                target = new ReferenceTarget(key.Kind, stableId);
                return true;
            }
            target = new ReferenceTarget(key.Kind, "");
            return false;
        }
    }

    private sealed record SourceContext(
        string NodeId,
        ProjectTreeNodeKind Kind,
        string TypeLabel,
        string Name,
        ReferenceUsageScope Scope,
        string ProjectId = "",
        ComponentOwner? ComponentOwner = null);

    private sealed record VariantOwner(string Id, string Name, string Reference, JsonObject Config);

    private sealed record ComponentOwner(
        string Id,
        string ProjectId,
        string ComponentType,
        string Name,
        JsonObject DesignPreview,
        IReadOnlyList<VariantOwner> Variants);

    private sealed record ComponentVariantOwner(ComponentOwner Owner, VariantOwner Variant);

    private enum RuntimeValueSource
    {
        DesignPreview,
        ProductionPayload,
        ExplicitValues,
    }

    private sealed record ModuleOwner(
        string Id,
        string ProjectId,
        string Name,
        JsonObject DesignPreview,
        IReadOnlyList<VariantOwner> Variants);
}
