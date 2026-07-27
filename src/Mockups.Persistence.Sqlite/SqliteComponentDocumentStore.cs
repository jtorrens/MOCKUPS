using Mockups.DesktopEditorShell.EditorShell;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed class SqliteComponentDocumentStore
{
    private readonly SqliteDesignOwner _design;
    private readonly ComponentFieldOptionResolver _fieldOptions;
    private readonly ReferenceUsageService _referenceUsages;

    internal SqliteComponentDocumentStore(
        SqliteDesignOwner design,
        ComponentFieldOptionResolver fieldOptions,
        ReferenceUsageService referenceUsages)
    {
        _design = design;
        _fieldOptions = fieldOptions;
        _referenceUsages = referenceUsages;
    }

    internal ComponentClassSettings GetComponentClassSettings(
        string componentClassId) =>
        _design.GetComponentClassSettings(componentClassId);

    internal ComponentClassSettings GetComponentVariantSettings(
        ProjectTreeNode variantNode) =>
        _design.GetComponentVariantSettings(variantNode);

    internal FieldValue CreateComponentClassFieldValue(
        string componentClassId,
        string fieldId)
    {
        var settings = _design.GetComponentClassSettings(componentClassId);
        return CreateComponentFieldValue(settings, fieldId);
    }

    internal FieldValue CreateComponentVariantFieldValue(
        ProjectTreeNode variantNode,
        string fieldId)
    {
        var settings = _design.GetComponentVariantSettings(variantNode);
        return CreateComponentFieldValue(settings, fieldId);
    }

    internal void UpdateComponentClassField(
        string componentClassId,
        string fieldId,
        string value) =>
        _design.UpdateComponentClassField(
            componentClassId,
            fieldId,
            value);

    internal void UpdateComponentVariantField(
        ProjectTreeNode variantNode,
        string fieldId,
        string value) =>
        _design.UpdateComponentVariantField(
            variantNode,
            fieldId,
            value);

    internal IReadOnlyList<ComponentVariantReferenceUsage>
        GetComponentVariantReferenceUsageDetails(ProjectTreeNode node) =>
        _referenceUsages.GetUsages(node.Kind, node.Id)
            .Select((usage) => new ComponentVariantReferenceUsage(
                usage.SourceTypeLabel,
                usage.SourceName,
                usage.FieldLabel,
                usage.SourceNodeId,
                usage.EmbeddedContext is null
                    ? null
                    : ToEmbeddedComponentUsage(
                        usage.EmbeddedContext)))
            .OrderBy(
                (usage) => usage.SourceKind,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                (usage) => usage.SourceName,
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                (usage) => usage.Detail,
                StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal IReadOnlyList<EmbeddedComponentUsage>
        GetEmbeddedComponentUsages(
            string projectId,
            string componentType,
            string? excludedComponentClassId = null) =>
        _design.GetEmbeddedComponentUsages(
            projectId,
            componentType,
            excludedComponentClassId);

    internal string GetEmbeddedComponentVariantName(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots) =>
        _design.GetEmbeddedComponentVariantName(ownerNode, slots);

    internal string GetRuntimeComponentVariantName(
        string variantReference,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots) =>
        _design.GetRuntimeComponentVariantName(
            variantReference,
            overrides,
            slots);

    internal FieldValue CreateEmbeddedComponentFieldValue(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
    {
        if (slots.Count == 0)
        {
            throw new InvalidOperationException(
                $"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var settings = ownerNode.Kind switch
        {
            ProjectTreeNodeKind.ComponentClass =>
                _design.GetComponentClassSettings(ownerNode.Id),
            ProjectTreeNodeKind.ComponentVariant =>
                _design.GetComponentVariantSettings(ownerNode),
            _ => null,
        };
        var moduleSettings = ownerNode.Kind switch
        {
            ProjectTreeNodeKind.Module =>
                _design.GetModuleSettings(ownerNode.Id),
            ProjectTreeNodeKind.ModuleVariant =>
                _design.GetModuleVariantSettings(ownerNode),
            _ => null,
        };
        if (settings is null && moduleSettings is null)
        {
            throw new InvalidOperationException(
                $"Embedded component field '{embeddedFieldId}' is not supported for '{ownerNode.Kind}'.");
        }

        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        var projectId = settings?.ProjectId ?? moduleSettings!.ProjectId;
        var options = _fieldOptions.Resolve(projectId, descriptor);
        return _design.CreateEmbeddedComponentFieldValue(
            ownerNode.Kind,
            projectId,
            settings?.ConfigJson ?? moduleSettings!.ConfigJson,
            slots,
            descriptor,
            options);
    }

    internal FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        JsonObject overrides,
        string fieldId)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var options = _fieldOptions.Resolve(projectId, descriptor);
        return _design.CreateRuntimeComponentOverrideFieldValue(
            baseConfigJson,
            overrides,
            descriptor,
            options);
    }

    internal FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string fieldId)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var options = _fieldOptions.Resolve(projectId, descriptor);
        return _design.CreateRuntimeComponentOverrideFieldValue(
            projectId,
            baseConfigJson,
            overrides,
            slots,
            descriptor,
            options);
    }

    internal FieldValue CreateEmbeddedComponentFieldValue(
        string componentClassId,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId)
    {
        var settings =
            _design.GetComponentClassSettings(componentClassId);
        var descriptor =
            ComponentClassFieldCatalog.Get(embeddedFieldId);
        var options = _fieldOptions.Resolve(
            settings.ProjectId,
            descriptor);
        return _design.CreateEmbeddedComponentFieldValue(
            settings,
            slotFieldId,
            embeddedComponentType,
            descriptor,
            options);
    }

    internal FieldValue CreateEmbeddedComponentFieldValue(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
    {
        if (slots.Count == 0)
        {
            throw new InvalidOperationException(
                $"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var settings =
            _design.GetComponentClassSettings(componentClassId);
        var descriptor =
            ComponentClassFieldCatalog.Get(embeddedFieldId);
        var options = _fieldOptions.Resolve(
            settings.ProjectId,
            descriptor);
        return _design.CreateEmbeddedComponentFieldValue(
            ProjectTreeNodeKind.ComponentClass,
            settings.ProjectId,
            settings.ConfigJson,
            slots,
            descriptor,
            options);
    }

    internal void UpdateEmbeddedComponentField(
        string componentClassId,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId,
        string value) =>
        _design.UpdateEmbeddedComponentField(
            componentClassId,
            slotFieldId,
            embeddedComponentType,
            embeddedFieldId,
            value);

    internal void UpdateEmbeddedComponentField(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value) =>
        _design.UpdateEmbeddedComponentField(
            componentClassId,
            slots,
            embeddedFieldId,
            value);

    internal void UpdateEmbeddedComponentField(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value) =>
        _design.UpdateEmbeddedComponentField(
            ownerNode,
            slots,
            embeddedFieldId,
            value);

    internal void UpdateRuntimeComponentOverride(
        JsonObject overrides,
        string fieldId,
        string value) =>
        _design.UpdateRuntimeComponentOverride(
            overrides,
            fieldId,
            value);

    internal void UpdateRuntimeComponentOverride(
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string fieldId,
        string value) =>
        _design.UpdateRuntimeComponentOverride(
            overrides,
            slots,
            fieldId,
            value);

    private FieldValue CreateComponentFieldValue(
        ComponentClassSettings settings,
        string fieldId)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var options = _fieldOptions.Resolve(
            settings.ProjectId,
            descriptor);
        return _design.CreateComponentFieldValue(
            settings,
            descriptor,
            options);
    }

    private static EmbeddedComponentUsage ToEmbeddedComponentUsage(
        ReferenceEmbeddedContext context) =>
        new(
            context.ParentComponentClassId,
            context.ParentComponentName,
            context.ParentComponentType,
            context.SlotFieldId,
            context.SlotLabel,
            context.HasOverrides,
            context.SourceNodeId);
}
