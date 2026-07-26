using Microsoft.Data.Sqlite;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteDesignOwner
{
    public IReadOnlyList<EmbeddedComponentUsage> GetEmbeddedComponentUsages(
        string projectId,
        string componentType,
        string? excludedComponentClassId = null)
    {
        using var connection = OpenConnection();
        var rows = _componentClassRepository
            .QueryByProject(connection, projectId)
            .Where((row) =>
                !row.Id.Equals(
                    excludedComponentClassId,
                    StringComparison.Ordinal))
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
            foreach (var variant in ComponentClassVariants(row.MetadataJson))
            {
                AddEmbeddedComponentUsages(
                    usages,
                    row,
                    $"{row.Name} · {variant.Name}",
                    VariantReferenceId.Format(row.Id, variant.Id),
                    variant.ConfigJson,
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
        ComponentClassDefinitionRecord row,
        string sourceName,
        string sourceNodeId,
        string configJson,
        string componentType)
    {
        var config = ParseJsonObject(configJson);
        foreach (var slot in EmbeddedComponentSlotCatalog.All()
                     .Where((candidate) =>
                         candidate.EmbeddedComponentType.Equals(
                             componentType,
                             StringComparison.Ordinal)))
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

    public string GetEmbeddedComponentVariantName(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots)
    {
        if (slots.Count == 0)
        {
            return "";
        }

        using var connection = OpenConnection();
        var settings = GetComponentClassSettings(
            connection,
            componentClassId);
        var ownerConfig = ParseJsonObject(settings.ConfigJson);
        return GetEmbeddedComponentVariantName(
            connection,
            settings.ProjectId,
            ownerConfig,
            slots);
    }

    public string GetEmbeddedComponentVariantName(
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
            ProjectTreeNodeKind.ComponentClass =>
                GetComponentClassSettings(
                    connection,
                    ownerNode.Id).ProjectId,
            ProjectTreeNodeKind.ComponentVariant =>
                GetComponentVariantSettings(
                    connection,
                    ownerNode).ProjectId,
            ProjectTreeNodeKind.Module =>
                GetModuleSettings(ownerNode.Id).ProjectId,
            ProjectTreeNodeKind.ModuleVariant =>
                GetModuleVariantSettings(ownerNode).ProjectId,
            _ => throw new InvalidOperationException(
                $"Embedded component variants are not supported for '{ownerNode.Kind}'."),
        };
        var ownerConfigJson = ownerNode.Kind switch
        {
            ProjectTreeNodeKind.Module =>
                GetModuleSettings(ownerNode.Id).ConfigJson,
            ProjectTreeNodeKind.ModuleVariant =>
                GetModuleVariantSettings(ownerNode).ConfigJson,
            ProjectTreeNodeKind.ComponentClass =>
                GetComponentClassSettings(
                    connection,
                    ownerNode.Id).ConfigJson,
            ProjectTreeNodeKind.ComponentVariant =>
                GetComponentVariantSettings(
                    connection,
                    ownerNode).ConfigJson,
            _ => throw new InvalidOperationException(
                $"Embedded component variants are not supported for '{ownerNode.Kind}'."),
        };
        var ownerConfig = ParseJsonObject(ownerConfigJson);
        return GetEmbeddedComponentVariantName(
            connection,
            projectId,
            ownerConfig,
            slots);
    }

    private string GetEmbeddedComponentVariantName(
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
                : JsonPath.Get(
                    currentContainer,
                    slot.SlotPath) as JsonObject;
            var variantReference = RequiredComponentVariantReference(
                slotNode,
                $"Embedded component slot '{slot.FieldId}'");
            if (index == slots.Count - 1)
            {
                return ComponentVariantName(
                    connection,
                    projectId,
                    slot.EmbeddedComponentType,
                    variantReference);
            }

            var child = ParseJsonObject(
                GetComponentClassVariantConfigJson(
                    connection,
                    projectId,
                    slot.EmbeddedComponentType,
                    variantReference));
            if (slotNode?["overrides"] is JsonObject overrides)
            {
                ComponentConfigOverrideMerger.MergeInto(
                    child,
                    overrides);
            }

            currentContainer = child;
        }

        return "";
    }

    public string GetComponentClassBaseConfigsJson(string projectId)
    {
        using var connection = OpenConnection();
        var configs = new JsonObject();
        var variants = new JsonObject();
        var variantTypes = new JsonObject();
        foreach (var row in _componentClassRepository
                     .QueryByProject(connection, projectId))
        {
            AddComponentVariantConfigs(connection, variants, row);
            foreach (var variant in RequiredComponentClassVariants(row))
            {
                variantTypes[
                    VariantReferenceId.Format(row.Id, variant.Id)] =
                    row.ComponentType;
            }

            if (configs.ContainsKey(row.ComponentType))
            {
                continue;
            }

            var defaultConfig = ParseJsonObject(
                DefaultComponentVariantConfigJson(
                    row.MetadataJson,
                    $"Component class '{row.Id}'"));
            ValidateEmbeddedSlotVariantReferences(
                connection,
                projectId,
                defaultConfig);
            configs[row.ComponentType] = defaultConfig;
        }

        configs["variants"] = variants;
        configs["variantTypes"] = variantTypes;
        return configs.ToJsonString();
    }

    public string ValidateComponentVariantReferencesForPreview(
        string projectId,
        string configJson)
    {
        using var connection = OpenConnection();
        var config = ParseJsonObject(configJson);
        ValidateEmbeddedSlotVariantReferences(
            connection,
            projectId,
            config);
        return config.ToJsonString();
    }

    private void AddComponentVariantConfigs(
        SqliteConnection connection,
        JsonObject target,
        ComponentClassDefinitionRecord row)
    {
        foreach (var variant in RequiredComponentClassVariants(row))
        {
            var config = ParseJsonObject(variant.ConfigJson);
            ValidateEmbeddedSlotVariantReferences(
                connection,
                row.ProjectId,
                config);
            target[
                VariantReferenceId.Format(row.Id, variant.Id)] =
                config;
        }
    }

    internal static string RequiredComponentVariantReference(
        JsonObject? slotNode,
        string owner)
    {
        if (slotNode?["variantReference"] is not JsonValue value
            || !value.TryGetValue<string>(out var reference)
            || string.IsNullOrWhiteSpace(reference))
        {
            throw new InvalidOperationException(
                $"{owner} requires a full component Variant reference.");
        }

        return reference;
    }

    public IReadOnlyList<FieldOption> GetComponentClassOptionsByType(
        string projectId,
        string componentType,
        bool includeNone = false)
    {
        using var connection = OpenConnection();
        var options = ComponentClassRowsByType(
                connection,
                projectId,
                componentType)
            .Select((row) => new FieldOption(row.Id, row.Name))
            .ToList();
        if (includeNone)
        {
            options.Insert(0, new FieldOption("", "None"));
        }

        return options;
    }

    public IReadOnlyList<FieldOption>
        GetComponentVariantReferenceOptionsByType(
            string projectId,
            string componentType,
            bool includeNone = false) =>
        GetComponentVariantReferenceOptions(
            projectId,
            componentType,
            includeNone);

    public IReadOnlyList<FieldOption>
        GetStatusBarComponentVariantOptions(string projectId) =>
        GetComponentVariantReferenceOptionsByType(
            projectId,
            "status_bar",
            includeNone: true);

    public IReadOnlyList<FieldOption>
        GetNavigationBarComponentVariantOptions(string projectId) =>
        GetComponentVariantReferenceOptionsByType(
            projectId,
            "navigation_bar",
            includeNone: true);

    public IReadOnlyList<FieldOption> GetComponentVariantReferenceOptions(
        string projectId,
        string componentTypeSelector,
        bool includeNone = false)
    {
        using var connection = OpenConnection();
        var selectorParts = componentTypeSelector.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries);
        var includeAll = selectorParts.Contains(
            "*",
            StringComparer.Ordinal);
        var includedTypes = selectorParts
            .Where((part) =>
                part != "*"
                && !part.StartsWith("-", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var excludedTypes = selectorParts
            .Where((part) =>
                part.StartsWith("-", StringComparison.Ordinal)
                && part.Length > 1)
            .Select((part) => part[1..])
            .ToHashSet(StringComparer.Ordinal);
        var rows = _componentClassRepository
            .QueryByProject(connection, projectId)
            .Where((row) => !excludedTypes.Contains(row.ComponentType))
            .Where((row) =>
                includeAll
                || includedTypes.Contains(row.ComponentType))
            .OrderBy(
                (row) => row.ComponentType,
                StringComparer.Ordinal)
            .ThenBy((row) => row.Name, StringComparer.Ordinal)
            .ToList();
        var showClassName =
            rows.Count > 1 || includeAll || includedTypes.Count > 1;
        var options = rows
            .SelectMany((row) =>
                RequiredComponentClassVariants(row)
                    .Select((variant) => new FieldOption(
                        VariantReferenceId.Format(row.Id, variant.Id),
                        showClassName
                            ? $"{row.Name} · {variant.Name}"
                            : variant.Name,
                        GroupValue: row.Id,
                        GroupLabel: row.Name,
                        LocalLabel: variant.Name)))
            .ToList();
        if (includeNone)
        {
            options.Insert(0, new FieldOption("", "None"));
        }

        return options;
    }

    public string ValidateComponentVariantReferenceValue(
        string projectId,
        string componentType,
        string reference,
        bool allowEmpty = false)
    {
        using var connection = OpenConnection();
        return ValidateComponentVariantReference(
            connection,
            projectId,
            componentType,
            reference,
            allowEmpty);
    }

    internal string DefaultComponentVariantReference(
        SqliteConnection connection,
        string projectId,
        string componentType)
    {
        var componentClass = ComponentClassRowsByType(
                connection,
                projectId,
                componentType)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Project '{projectId}' has no {componentType} component class.");
        var variant = RequiredComponentClassVariants(componentClass)
            .FirstOrDefault((candidate) =>
                candidate.Id.Equals(
                    VariantEnvelopeContract.DefaultId,
                    StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Component class '{componentClass.Id}' has no protected default variant.");
        return VariantReferenceId.Format(
            componentClass.Id,
            variant.Id);
    }

    private List<ComponentClassDefinitionRecord> ComponentClassRowsByType(
        SqliteConnection connection,
        string projectId,
        string componentType) =>
        _componentClassRepository
            .QueryByProject(connection, projectId)
            .Where((row) =>
                row.ComponentType.Equals(
                    componentType,
                    StringComparison.Ordinal))
            .OrderBy((row) =>
                row.Id.Equals(
                    $"component_{projectId}_{componentType}",
                    StringComparison.Ordinal)
                    ? 0
                    : 1)
            .ThenBy((row) => row.Name, StringComparer.Ordinal)
            .ToList();

    internal static IReadOnlyList<ComponentClassVariant>
        RequiredComponentClassVariants(
            ComponentClassDefinitionRecord row) =>
        ComponentClassVariants(
            row.MetadataJson,
            $"Component class '{row.Id}'");

    internal string GetComponentClassVariantConfigJson(
        SqliteConnection connection,
        string projectId,
        string componentType,
        string variantReference)
    {
        if (!VariantReferenceId.TryParse(
                variantReference,
                out var componentClassId,
                out var referencedVariantId))
        {
            throw new InvalidOperationException(
                $"Component variant reference '{variantReference}' must use the full componentClassId::variant::variantId form.");
        }

        var referencedRow = _componentClassRepository
            .QueryByProject(connection, projectId)
            .FirstOrDefault((row) =>
                row.Id.Equals(
                    componentClassId,
                    StringComparison.Ordinal)
                && row.ComponentType.Equals(
                    componentType,
                    StringComparison.Ordinal));
        if (referencedRow is null)
        {
            throw new InvalidOperationException(
                $"Missing component variant reference '{variantReference}'.");
        }

        return ComponentVariantConfigJson(
            referencedRow,
            referencedVariantId);
    }

    private static string ComponentVariantConfigJson(
        ComponentClassDefinitionRecord row,
        string variantId) =>
        RequiredComponentClassVariants(row)
            .Single((candidate) =>
                candidate.Id.Equals(
                    variantId,
                    StringComparison.Ordinal))
            .ConfigJson;

    internal IReadOnlyList<FieldOption> EmbeddedComponentOptions(
        string projectId,
        string recordClassId)
    {
        using var connection = OpenConnection();
        var name = _componentClassRepository
            .QueryByProject(connection, projectId)
            .FirstOrDefault((row) =>
                row.RecordClassId.Equals(
                    recordClassId,
                    StringComparison.Ordinal))
            ?.Name;
        return
        [
            new FieldOption(
                recordClassId,
                string.IsNullOrWhiteSpace(name)
                    ? recordClassId
                    : name),
        ];
    }

    private string ComponentVariantName(
        SqliteConnection connection,
        string projectId,
        string componentType,
        string variantReference)
    {
        if (!VariantReferenceId.TryParse(
                variantReference,
                out var componentClassId,
                out var referencedVariantId))
        {
            throw new InvalidOperationException(
                $"Component variant reference '{variantReference}' must use the full componentClassId::variant::variantId form.");
        }

        var row = _componentClassRepository
            .QueryByProject(connection, projectId)
            .FirstOrDefault((candidate) =>
                candidate.Id.Equals(
                    componentClassId,
                    StringComparison.Ordinal)
                && candidate.ComponentType.Equals(
                    componentType,
                    StringComparison.Ordinal));
        if (row is null)
        {
            return variantReference;
        }

        var referencedVariant = RequiredComponentClassVariants(row)
            .FirstOrDefault((candidate) =>
                candidate.Id.Equals(
                    referencedVariantId,
                    StringComparison.Ordinal));
        var variantName = string.IsNullOrWhiteSpace(
            referencedVariant?.Name)
                ? referencedVariantId
                : referencedVariant.Name;
        return $"{row.Name} · {variantName}";
    }

    internal static bool EmbeddedComponentHasOverrides(
        string configJson,
        EmbeddedComponentSlotDefinition slot)
    {
        var config = ParseJsonObject(configJson);
        if (JsonPath.Get(config, slot.SlotPath)
                is not JsonObject slotNode
            || slotNode["overrides"] is not JsonObject overrides)
        {
            return false;
        }

        return HasEffectiveJsonValue(overrides);
    }

    internal static bool HasEffectiveJsonValue(JsonObject value)
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
