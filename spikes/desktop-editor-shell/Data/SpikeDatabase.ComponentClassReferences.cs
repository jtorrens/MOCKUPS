using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    public sealed record ComponentPresetSelectionSettings(
        string ProjectId,
        string ComponentType,
        string RecordClassId,
        string ConfigJson);


    public IReadOnlyList<EmbeddedComponentUsage> GetEmbeddedComponentUsages(
        string projectId,
        string componentType,
        string? excludedComponentClassId = null)
    {
        using var connection = OpenConnection();
        var rows = QueryComponentClassRows(connection)
            .Where((row) => row.ProjectId.Equals(projectId, StringComparison.Ordinal))
            .Where((row) => !row.Id.Equals(excludedComponentClassId, StringComparison.Ordinal))
            .ToList();
        var usages = new List<EmbeddedComponentUsage>();
        foreach (var row in rows)
        {
            AddEmbeddedComponentUsages(
                usages,
                row,
                row.Name,
                row.Id,
                row.ConfigJson,
                componentType);
            foreach (var preset in ComponentClassPresets(row.MetadataJson))
            {
                AddEmbeddedComponentUsages(
                    usages,
                    row,
                    $"{row.Name} · {preset.Name}",
                    ComponentPresetNodeId(row.Id, preset.Id),
                    preset.ConfigJson,
                    componentType);
            }
        }

        return usages
            .OrderBy((usage) => usage.ParentComponentType)
            .ThenBy((usage) => usage.ParentComponentName)
            .ThenBy((usage) => usage.SlotLabel)
            .ToList();
    }

    private static void AddEmbeddedComponentUsages(
        ICollection<EmbeddedComponentUsage> usages,
        ComponentClassRow row,
        string sourceName,
        string sourceNodeId,
        string configJson,
        string componentType)
    {
        var config = ParseJsonObject(configJson);
        foreach (var slot in EmbeddedComponentSlotCatalog.All()
                     .Where((candidate) => candidate.EmbeddedComponentType.Equals(componentType, StringComparison.Ordinal)))
        {
            if (JsonPath.Get(config, slot.SlotPath) is not JsonObject)
            {
                continue;
            }

            usages.Add(new EmbeddedComponentUsage(
                row.Id,
                sourceName,
                row.ComponentType,
                slot.FieldId,
                slot.Label,
                EmbeddedComponentHasOverrides(configJson, slot),
                sourceNodeId));
        }
    }

    public string GetEmbeddedComponentPresetName(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        if (slots.Count == 0)
        {
            return "";
        }

        using var connection = OpenConnection();
        var settings = GetComponentClassSettings(connection, componentClassId);
        var ownerConfig = ParseJsonObject(settings.ConfigJson);
        return GetEmbeddedComponentPresetName(connection, settings.ProjectId, ownerConfig, slots);
    }

    public string GetEmbeddedComponentPresetName(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        if (slots.Count == 0)
        {
            return "";
        }

        using var connection = OpenConnection();
        var projectId = ownerNode.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass => GetComponentClassSettings(connection, ownerNode.Id).ProjectId,
            ProjectTreeNodeKind.ComponentPreset => GetComponentPresetSettings(connection, ownerNode).ProjectId,
            ProjectTreeNodeKind.Module => GetModuleSettings(ownerNode.Id).ProjectId,
            ProjectTreeNodeKind.ModuleVariant => GetModuleVariantSettings(ownerNode).ProjectId,
            _ => throw new InvalidOperationException($"Embedded component variants are not supported for '{ownerNode.Kind}'."),
        };
        var ownerConfigJson = ownerNode.Kind is ProjectTreeNodeKind.Module or ProjectTreeNodeKind.ModuleVariant
            ? ownerNode.Kind == ProjectTreeNodeKind.Module
                ? GetModuleSettings(ownerNode.Id).ConfigJson
                : GetModuleVariantSettings(ownerNode).ConfigJson
            : ownerNode.Kind == ProjectTreeNodeKind.ComponentClass
                ? GetComponentClassSettings(connection, ownerNode.Id).ConfigJson
                : GetComponentPresetSettings(connection, ownerNode).ConfigJson;
        var ownerConfig = ParseJsonObject(ownerConfigJson);
        return GetEmbeddedComponentPresetName(connection, projectId, ownerConfig, slots);
    }

    private static string GetEmbeddedComponentPresetName(
        SqliteConnection connection,
        string projectId,
        JsonObject ownerConfig,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        JsonObject? currentContainer = ownerConfig;
        for (var index = 0; index < slots.Count; index++)
        {
            var slot = slots[index];
            var slotNode = currentContainer is null
                ? null
                : JsonPath.Get(currentContainer, slot.SlotPath) as JsonObject;
            var presetId = JsonPath.String(slotNode ?? [], "presetId", DefaultComponentPresetId);
            if (index == slots.Count - 1)
            {
                return ComponentPresetName(connection, projectId, slot.EmbeddedComponentType, presetId);
            }

            var child = ParseJsonObject(GetComponentClassPresetConfigJson(
                connection,
                projectId,
                slot.EmbeddedComponentType,
                presetId));
            if (slotNode?["overrides"] is JsonObject overrides)
            {
                MergeOverride(child, overrides);
            }

            currentContainer = child;
        }

        return "";
    }

    public string GetComponentClassBaseConfigsJson(string projectId)
    {
        using var connection = OpenConnection();
        var configs = new JsonObject();
        var presets = new JsonObject();
        var presetTypes = new JsonObject();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, component_type, config_json, metadata_json
            FROM component_classes
            WHERE project_id = $projectId
            ORDER BY CASE WHEN id = 'component_' || $projectId || '_' || component_type THEN 0 ELSE 1 END, name
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var componentClassId = reader.GetString(0);
            var componentType = reader.GetString(1);
            var classConfigJson = ReadString(reader, 2);
            var metadataJson = ReadString(reader, 3);
            var row = new ComponentClassRow(
                componentClassId,
                projectId,
                componentType,
                "",
                "",
                "",
                classConfigJson,
                "",
                metadataJson);
            AddComponentPresetConfigs(connection, presets, row);
            foreach (var preset in RequiredComponentClassPresets(row))
            {
                presetTypes[ComponentPresetNodeId(componentClassId, preset.Id)] = componentType;
            }
            if (configs.ContainsKey(componentType))
            {
                continue;
            }

            var defaultConfig = ParseJsonObject(DefaultComponentPresetConfigJson(
                metadataJson,
                $"Component class '{componentClassId}'"));
            ValidateEmbeddedSlotPresetReferences(connection, projectId, defaultConfig);
            configs[componentType] = defaultConfig;
        }

        configs["presets"] = presets;
        configs["presetTypes"] = presetTypes;
        return configs.ToJsonString();
    }

    public string ValidateComponentPresetReferencesForPreview(string projectId, string configJson)
    {
        using var connection = OpenConnection();
        var config = ParseJsonObject(configJson);
        ValidateEmbeddedSlotPresetReferences(connection, projectId, config);
        return config.ToJsonString();
    }

    private static void AddComponentPresetConfigs(SqliteConnection connection, JsonObject target, ComponentClassRow row)
    {
        foreach (var preset in RequiredComponentClassPresets(row))
        {
            var config = ParseJsonObject(preset.ConfigJson);
            ValidateEmbeddedSlotPresetReferences(connection, row.ProjectId, config);
            target[ComponentPresetNodeId(row.Id, preset.Id)] = config;
        }
    }

    private static void ValidateEmbeddedSlotPresetReferences(
        SqliteConnection connection,
        string projectId,
        JsonObject config)
    {
        var componentRows = QueryComponentClassRows(connection)
            .Where((row) => row.ProjectId.Equals(projectId, StringComparison.Ordinal))
            .ToList();

        foreach (var slot in EmbeddedComponentSlotCatalog.All())
        {
            if (JsonPath.Get(config, slot.SlotPath) is not JsonObject slotNode)
            {
                continue;
            }

            var reference = JsonPath.String(slotNode, "presetId", "");
            if (!TryParseComponentPresetNodeId(reference, out var componentClassId, out var presetId))
            {
                throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' must use a full component variant reference.");
            }

            var componentClass = componentRows.FirstOrDefault((row) =>
                row.Id.Equals(componentClassId, StringComparison.Ordinal)
                && row.ComponentType.Equals(slot.EmbeddedComponentType, StringComparison.Ordinal));
            if (componentClass is null)
            {
                throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' references missing {slot.EmbeddedComponentType} class '{componentClassId}'.");
            }

            if (!RequiredComponentClassPresets(componentClass)
                    .Any((preset) => preset.Id.Equals(presetId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Embedded component slot '{slot.FieldId}' references missing variant '{presetId}' on '{componentClassId}'.");
            }
        }

    }

    public IReadOnlyList<FieldOption> GetComponentClassOptionsByType(
        string projectId,
        string componentType,
        bool includeNone = false)
    {
        using var connection = OpenConnection();
        var options = QueryComponentClassRows(connection)
            .Where((row) => row.ProjectId.Equals(projectId, StringComparison.Ordinal))
            .Where((row) => row.ComponentType.Equals(componentType, StringComparison.Ordinal))
            .OrderBy((row) => row.Id.Equals($"component_{projectId}_{componentType}", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy((row) => row.Name, StringComparer.Ordinal)
            .Select((row) => new FieldOption(row.Id, row.Name))
            .ToList();
        if (includeNone)
        {
            options.Insert(0, new FieldOption("", "None"));
        }

        return options;
    }

    public IReadOnlyList<FieldOption> GetComponentPresetReferenceOptionsByType(
        string projectId,
        string componentType,
        bool includeNone = false)
    {
        return GetComponentPresetReferenceOptions(projectId, componentType, includeNone);
    }

    public IReadOnlyList<FieldOption> GetComponentPresetReferenceOptions(
        string projectId,
        string componentTypeSelector,
        bool includeNone = false)
    {
        using var connection = OpenConnection();
        var selectorParts = componentTypeSelector.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var includeAll = selectorParts.Contains("*", StringComparer.Ordinal);
        var includedTypes = selectorParts
            .Where((part) => part != "*" && !part.StartsWith("-", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var excludedTypes = selectorParts
            .Where((part) => part.StartsWith("-", StringComparison.Ordinal) && part.Length > 1)
            .Select((part) => part[1..])
            .ToHashSet(StringComparer.Ordinal);
        var rows = QueryComponentClassRows(connection)
            .Where((row) => row.ProjectId.Equals(projectId, StringComparison.Ordinal))
            .Where((row) => !excludedTypes.Contains(row.ComponentType))
            .Where((row) => includeAll || includedTypes.Contains(row.ComponentType))
            .OrderBy((row) => row.ComponentType, StringComparer.Ordinal)
            .ThenBy((row) => row.Name, StringComparer.Ordinal)
            .ToList();
        var showClassName = rows.Count > 1 || includeAll || includedTypes.Count > 1;
        var options = rows
            .SelectMany((row) => RequiredComponentClassPresets(row)
                .Select((preset) => new FieldOption(
                    ComponentPresetNodeId(row.Id, preset.Id),
                    showClassName ? $"{row.Name} · {preset.Name}" : preset.Name,
                    GroupValue: row.Id,
                    GroupLabel: row.Name,
                    LocalLabel: preset.Name)))
            .ToList();
        if (includeNone)
        {
            options.Insert(0, new FieldOption("", "None"));
        }

        return options;
    }

    public JsonObject GetComponentPresetRuntimeInputs(string presetReference)
    {
        var effective = GetComponentPresetRuntimeContract(presetReference);
        return ParseJsonObject(DesignPreviewTestValues.RuntimeJson(effective.ToJsonString()));
    }

    public JsonObject GetComponentPresetRuntimeContract(string presetReference)
    {
        if (!TryParseComponentPresetNodeId(presetReference, out var componentClassId, out _))
        {
            throw new InvalidOperationException($"Invalid component Variant reference '{presetReference}'.");
        }

        var settings = GetComponentClassSettings(componentClassId);
        var config = GetComponentPresetConfig(presetReference);
        var effective = RuntimeInputForwardingContract.EffectivePreview(
            ParseJsonObject(settings.DesignPreviewJson),
            config);
        return effective;
    }

    public IReadOnlyList<ComponentInputBindingDefinition> GetComponentPresetRuntimeInputBindings(
        string presetReference)
    {
        if (!TryParseComponentPresetNodeId(presetReference, out var componentClassId, out _))
        {
            return [];
        }
        var settings = GetComponentClassSettings(componentClassId);
        var config = GetComponentPresetConfig(presetReference);
        var effective = RuntimeInputForwardingContract.EffectivePreview(
            ParseJsonObject(settings.DesignPreviewJson),
            config);
        return ComponentPreviewInputSession.ReadRuntimeInputs(effective, config)
            .Select((input) => new ComponentInputBindingDefinition(
                input.Id,
                input.Label,
                input.JsonKey,
                input.ValueKind,
                ComponentInputBindingSource.Variant,
                input.DefaultValue,
                input.Options,
                new NumberDefinition(input.Minimum, input.Maximum, input.Increment),
                input.ComponentType,
                input.UiGroupId,
                input.UiGroupLabel,
                input.TableId,
                input.ResolvedJsonKey,
                input.UiParentGroupId,
                input.UiOrder,
                input.UiSectionLabel,
                input.Transition,
                input.Animation,
                input.BehaviorTiming,
                input.ActionOnly))
            .ToList();
    }

    public IReadOnlyList<RuntimeInputCollectionDefinition> GetComponentPresetRuntimeCollections(
        string presetReference)
    {
        if (!TryParseComponentPresetNodeId(presetReference, out var componentClassId, out _))
        {
            return [];
        }
        var settings = GetComponentClassSettings(componentClassId);
        var config = GetComponentPresetConfig(presetReference);
        var effective = RuntimeInputForwardingContract.EffectivePreview(
            ParseJsonObject(settings.DesignPreviewJson),
            config);
        return ComponentPreviewInputSession.ReadRuntimeCollections(effective, config);
    }

    public JsonObject GetComponentPresetConfig(string presetReference)
    {
        if (!TryParseComponentPresetNodeId(presetReference, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component Variant reference '{presetReference}'.");
        }

        using var connection = OpenConnection();
        var row = QueryComponentClassRows(connection)
            .Single((candidate) => candidate.Id.Equals(componentClassId, StringComparison.Ordinal));
        var preset = RequiredComponentClassPresets(row)
            .Single((candidate) => candidate.Id.Equals(presetId, StringComparison.Ordinal));
        return ParseJsonObject(preset.ConfigJson);
    }

    public ComponentPresetSelectionSettings GetComponentPresetSelectionSettings(string presetReference)
    {
        if (!TryParseComponentPresetNodeId(presetReference, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component Variant reference '{presetReference}'.");
        }

        using var connection = OpenConnection();
        var row = QueryComponentClassRows(connection)
            .Single((candidate) => candidate.Id.Equals(componentClassId, StringComparison.Ordinal));
        var preset = RequiredComponentClassPresets(row)
            .Single((candidate) => candidate.Id.Equals(presetId, StringComparison.Ordinal));
        return new ComponentPresetSelectionSettings(
            row.ProjectId,
            row.ComponentType,
            row.RecordClassId,
            preset.ConfigJson);
    }

    public string GetRuntimeComponentPresetName(
        string presetReference,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        if (!TryParseComponentPresetNodeId(presetReference, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException($"Invalid component Variant reference '{presetReference}'.");
        }

        using var connection = OpenConnection();
        var row = QueryComponentClassRows(connection)
            .Single((candidate) => candidate.Id.Equals(componentClassId, StringComparison.Ordinal));
        var preset = RequiredComponentClassPresets(row)
            .Single((candidate) => candidate.Id.Equals(presetId, StringComparison.Ordinal));
        if (slots.Count == 0) return preset.Name;
        var ownerConfig = ParseJsonObject(preset.ConfigJson);
        MergeOverride(ownerConfig, overrides);
        return GetEmbeddedComponentPresetName(connection, row.ProjectId, ownerConfig, slots);
    }

    public string ValidateComponentPresetReferenceValue(
        string projectId,
        string componentType,
        string reference,
        bool allowEmpty = false)
    {
        using var connection = OpenConnection();
        return ValidateComponentPresetReference(connection, projectId, componentType, reference, allowEmpty);
    }

    private static string DefaultComponentPresetReference(
        SqliteConnection connection,
        string projectId,
        string componentType)
    {
        var rows = ComponentClassRowsByType(connection, projectId, componentType);
        var componentClass = rows.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Project '{projectId}' has no {componentType} component class.");
        var preset = RequiredComponentClassPresets(componentClass)
            .FirstOrDefault((candidate) => candidate.Id.Equals(DefaultComponentPresetId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Component class '{componentClass.Id}' has no protected default variant.");
        return ComponentPresetNodeId(componentClass.Id, preset.Id);
    }

    private static string ValidateComponentPresetReference(
        SqliteConnection connection,
        string projectId,
        string componentType,
        string reference,
        bool allowEmpty = false)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            if (allowEmpty)
            {
                return "";
            }

            throw new InvalidOperationException(
                $"A {componentType} component variant reference is required.");
        }

        if (!TryParseComponentPresetNodeId(reference, out var componentClassId, out var presetId))
        {
            throw new InvalidOperationException(
                $"Component variant reference '{reference}' must use the full componentClassId::preset::presetId form.");
        }

        var componentClass = ComponentClassRowsByType(connection, projectId, componentType)
            .FirstOrDefault((candidate) => candidate.Id.Equals(componentClassId, StringComparison.Ordinal));
        if (componentClass is null)
        {
            throw new InvalidOperationException(
                $"Component variant reference '{reference}' does not name a {componentType} class in project '{projectId}'.");
        }

        if (!RequiredComponentClassPresets(componentClass)
                .Any((candidate) => candidate.Id.Equals(presetId, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Component variant reference '{reference}' names a missing variant on '{componentClassId}'.");
        }

        return reference;
    }

    private static List<ComponentClassRow> ComponentClassRowsByType(
        SqliteConnection connection,
        string projectId,
        string componentType)
    {
        return QueryComponentClassRows(connection)
            .Where((row) => row.ProjectId.Equals(projectId, StringComparison.Ordinal))
            .Where((row) => row.ComponentType.Equals(componentType, StringComparison.Ordinal))
            .OrderBy((row) => row.Id.Equals($"component_{projectId}_{componentType}", StringComparison.Ordinal) ? 0 : 1)
            .ThenBy((row) => row.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<ComponentClassPreset> RequiredComponentClassPresets(ComponentClassRow row)
    {
        return ComponentClassPresets(row.MetadataJson, $"Component class '{row.Id}'");
    }

    private static string PreferredPresetId(ComponentClassRow row)
    {
        var presets = RequiredComponentClassPresets(row);
        return presets.FirstOrDefault((preset) => preset.Id.Equals(DefaultComponentPresetId, StringComparison.Ordinal))?.Id
            ?? presets[0].Id;
    }

    private static string GetComponentClassPresetConfigJson(
        SqliteConnection connection,
        string projectId,
        string componentType,
        string presetReference)
    {
        if (!TryParseComponentPresetNodeId(presetReference, out var componentClassId, out var referencedPresetId))
        {
            throw new InvalidOperationException(
                $"Component variant reference '{presetReference}' must use the full componentClassId::preset::presetId form.");
        }

        var referencedRow = QueryComponentClassRows(connection)
            .FirstOrDefault((row) =>
                row.ProjectId.Equals(projectId, StringComparison.Ordinal)
                && row.Id.Equals(componentClassId, StringComparison.Ordinal)
                && row.ComponentType.Equals(componentType, StringComparison.Ordinal));
        if (referencedRow is null)
        {
            throw new InvalidOperationException($"Missing component variant reference '{presetReference}'.");
        }

        return ComponentPresetConfigJson(referencedRow, referencedPresetId);
    }

    private static string ComponentPresetConfigJson(ComponentClassRow row, string presetId)
    {
        return RequiredComponentClassPresets(row)
            .Single((candidate) => candidate.Id.Equals(presetId, StringComparison.Ordinal))
            .ConfigJson;
    }

    private IReadOnlyList<FieldOption> EmbeddedComponentOptions(string projectId, string recordClassId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name
            FROM component_classes
            WHERE project_id = $projectId
              AND record_class_id = $recordClassId
            ORDER BY CASE WHEN id = 'component_' || $projectId || '_' || component_type THEN 0 ELSE 1 END, name
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$projectId", projectId);
        command.Parameters.AddWithValue("$recordClassId", recordClassId);
        var name = command.ExecuteScalar() as string;
        return [new FieldOption(recordClassId, string.IsNullOrWhiteSpace(name) ? recordClassId : name)];
    }

    private IReadOnlyList<FieldOption> ComponentPresetOptions(string projectId, string componentType)
    {
        return GetComponentPresetReferenceOptionsByType(projectId, componentType);
    }

    private static string ComponentPresetName(
        SqliteConnection connection,
        string projectId,
        string componentType,
        string presetReference)
    {
        if (TryParseComponentPresetNodeId(presetReference, out var componentClassId, out var referencedPresetId))
        {
            var row = QueryComponentClassRows(connection)
                .FirstOrDefault((candidate) =>
                    candidate.ProjectId.Equals(projectId, StringComparison.Ordinal)
                    && candidate.Id.Equals(componentClassId, StringComparison.Ordinal)
                    && candidate.ComponentType.Equals(componentType, StringComparison.Ordinal));
            if (row is null)
            {
                return presetReference;
            }

            var referencedPreset = RequiredComponentClassPresets(row)
                .FirstOrDefault((candidate) => candidate.Id.Equals(referencedPresetId, StringComparison.Ordinal));
            var presetName = string.IsNullOrWhiteSpace(referencedPreset?.Name) ? referencedPresetId : referencedPreset.Name;
            return $"{row.Name} · {presetName}";
        }

        throw new InvalidOperationException(
            $"Component variant reference '{presetReference}' must use the full componentClassId::preset::presetId form.");
    }

    private IReadOnlyList<FieldOption>? ComponentClassFieldOptions(
        string projectId,
        ComponentClassFieldDescriptor descriptor)
    {
        return descriptor.ValueKind switch
        {
            ValueKind.EmbeddedComponent => EmbeddedComponentOptions(projectId, descriptor.DefaultValue),
            ValueKind.ComponentPreset when EmbeddedComponentSlotCatalog.TryGet(descriptor.Id, out var slot)
                => ComponentPresetOptions(projectId, slot.EmbeddedComponentType),
            ValueKind.ComponentPreset when !string.IsNullOrWhiteSpace(descriptor.ComponentPresetType)
                => ComponentPresetOptions(projectId, descriptor.ComponentPresetType),
            ValueKind.OptionToken when !string.IsNullOrWhiteSpace(descriptor.ComponentPresetType)
                => ComponentPresetOptions(projectId, descriptor.ComponentPresetType),
            ValueKind.OptionToken when EmbeddedComponentPresetType(descriptor.Id) is { } componentType
                => ComponentPresetOptions(projectId, componentType),
            ValueKind.PaletteColorToken or ValueKind.PaletteColorPair or ValueKind.PaletteColorAlphaPair
                => GetPaletteColorOptions(projectId),
            ValueKind.TypographyStyle
                => [new FieldOption("theme", "Theme"), .. GetProductionFontOptions(projectId, "text")],
            _ => descriptor.Options,
        };
    }

    private static string? EmbeddedComponentPresetType(string fieldId)
    {
        if (!fieldId.EndsWith(".presetId", StringComparison.Ordinal))
        {
            return null;
        }

        var slotEditorFieldId = string.Concat(fieldId.AsSpan(0, fieldId.Length - ".presetId".Length), ".editor");
        return EmbeddedComponentSlotCatalog.TryGet(slotEditorFieldId, out var slot)
            ? slot.EmbeddedComponentType
            : null;
    }

    private static bool EmbeddedComponentHasOverrides(
        string configJson,
        EmbeddedComponentSlotDefinition slot)
    {
        var config = ParseJsonObject(configJson);
        var overrides = EmbeddedOverrides(config, slot, createIfMissing: false);
        return overrides is not null && HasEffectiveJsonValue(overrides);
    }

    private static bool HasEffectiveJsonValue(JsonObject value)
    {
        foreach (var child in value)
        {
            if (child.Value is JsonObject childObject)
            {
                if (HasEffectiveJsonValue(childObject))
                {
                    return true;
                }

                continue;
            }

            if (child.Value is not null)
            {
                return true;
            }
        }

        return false;
    }

}
