using Mockups.DesktopEditorShell.EditorShell;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SqliteProjectEngine
{
    public ComponentClassSettings GetComponentClassSettings(
        string componentClassId) =>
        _designOwner.GetComponentClassSettings(componentClassId);

    public ComponentClassSettings GetComponentVariantSettings(
        ProjectTreeNode variantNode) =>
        _designOwner.GetComponentVariantSettings(variantNode);

    public void UpdateComponentClassDesignPreviewJson(
        string componentClassId,
        string designPreviewJson) =>
        _designOwner.UpdateComponentClassDesignPreviewJson(
            componentClassId,
            designPreviewJson);

    public FieldValue CreateComponentClassFieldValue(
        string componentClassId,
        string fieldId)
    {
        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var options = _componentFieldOptions.Resolve(
            settings.ProjectId,
            descriptor);
        return _designOwner.CreateComponentFieldValue(
            settings,
            descriptor,
            options);
    }

    public FieldValue CreateComponentVariantFieldValue(
        ProjectTreeNode variantNode,
        string fieldId)
    {
        var settings = GetComponentVariantSettings(variantNode);
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var options = _componentFieldOptions.Resolve(
            settings.ProjectId,
            descriptor);
        return _designOwner.CreateComponentFieldValue(
            settings,
            descriptor,
            options);
    }

    public FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        JsonObject overrides,
        string fieldId)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var options = _componentFieldOptions.Resolve(
            projectId,
            descriptor);
        return _designOwner.CreateRuntimeComponentOverrideFieldValue(
            baseConfigJson,
            overrides,
            descriptor,
            options);
    }

    public FieldValue CreateRuntimeComponentOverrideFieldValue(
        string projectId,
        string baseConfigJson,
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string fieldId)
    {
        var descriptor = ComponentClassFieldCatalog.Get(fieldId);
        var options = _componentFieldOptions.Resolve(
            projectId,
            descriptor);
        return _designOwner.CreateRuntimeComponentOverrideFieldValue(
            projectId,
            baseConfigJson,
            overrides,
            slots,
            descriptor,
            options);
    }

    public void UpdateRuntimeComponentOverride(
        JsonObject overrides,
        string fieldId,
        string value) =>
        _designOwner.UpdateRuntimeComponentOverride(
            overrides,
            fieldId,
            value);

    public void UpdateRuntimeComponentOverride(
        JsonObject overrides,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string fieldId,
        string value) =>
        _designOwner.UpdateRuntimeComponentOverride(
            overrides,
            slots,
            fieldId,
            value);

    public void UpdateComponentClassField(
        string componentClassId,
        string fieldId,
        string value) =>
        _designOwner.UpdateComponentClassField(
            componentClassId,
            fieldId,
            value);

    public void UpdateComponentVariantField(
        ProjectTreeNode variantNode,
        string fieldId,
        string value) =>
        _designOwner.UpdateComponentVariantField(
            variantNode,
            fieldId,
            value);

    public FieldValue CreateEmbeddedComponentFieldValue(
        string componentClassId,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId)
    {
        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        var options = _componentFieldOptions.Resolve(
            settings.ProjectId,
            descriptor);
        return _designOwner.CreateEmbeddedComponentFieldValue(
            settings,
            slotFieldId,
            embeddedComponentType,
            descriptor,
            options);
    }

    public FieldValue CreateEmbeddedComponentFieldValue(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
    {
        if (slots.Count == 0)
        {
            throw new InvalidOperationException(
                $"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        var settings = GetComponentClassSettings(componentClassId);
        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        var options = _componentFieldOptions.Resolve(
            settings.ProjectId,
            descriptor);
        return _designOwner.CreateEmbeddedComponentFieldValue(
            ProjectTreeNodeKind.ComponentClass,
            settings.ProjectId,
            settings.ConfigJson,
            slots,
            descriptor,
            options);
    }

    public FieldValue CreateEmbeddedComponentFieldValue(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId)
    {
        if (ownerNode.Kind == ProjectTreeNodeKind.ComponentClass)
        {
            return CreateEmbeddedComponentFieldValue(
                ownerNode.Id,
                slots,
                embeddedFieldId);
        }

        if (slots.Count == 0)
        {
            throw new InvalidOperationException(
                $"Embedded component field '{embeddedFieldId}' needs at least one slot.");
        }

        ComponentClassSettings? componentSettings = null;
        ModuleSettings? moduleSettings = null;
        if (ownerNode.Kind is
            ProjectTreeNodeKind.Module
            or ProjectTreeNodeKind.ModuleVariant)
        {
            moduleSettings = ownerNode.Kind == ProjectTreeNodeKind.Module
                ? GetModuleSettings(ownerNode.Id)
                : GetModuleVariantSettings(ownerNode);
        }
        else if (ownerNode.Kind == ProjectTreeNodeKind.ComponentVariant)
        {
            componentSettings =
                GetComponentVariantSettings(ownerNode);
        }
        else
        {
            throw new InvalidOperationException(
                $"Embedded component field '{embeddedFieldId}' is not supported for '{ownerNode.Kind}'.");
        }

        var descriptor = ComponentClassFieldCatalog.Get(embeddedFieldId);
        var projectId = componentSettings?.ProjectId
            ?? moduleSettings!.ProjectId;
        var configJson = componentSettings?.ConfigJson
            ?? moduleSettings!.ConfigJson;
        var options = _componentFieldOptions.Resolve(
            projectId,
            descriptor);
        return _designOwner.CreateEmbeddedComponentFieldValue(
            ownerNode.Kind,
            projectId,
            configJson,
            slots,
            descriptor,
            options);
    }

    public void UpdateEmbeddedComponentField(
        string componentClassId,
        string slotFieldId,
        string embeddedComponentType,
        string embeddedFieldId,
        string value) =>
        _designOwner.UpdateEmbeddedComponentField(
            componentClassId,
            slotFieldId,
            embeddedComponentType,
            embeddedFieldId,
            value);

    public void UpdateEmbeddedComponentField(
        string componentClassId,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value) =>
        _designOwner.UpdateEmbeddedComponentField(
            componentClassId,
            slots,
            embeddedFieldId,
            value);

    public void UpdateEmbeddedComponentField(
        ProjectTreeNode ownerNode,
        IReadOnlyList<EmbeddedComponentSlotDefinition> slots,
        string embeddedFieldId,
        string value) =>
        _designOwner.UpdateEmbeddedComponentField(
            ownerNode,
            slots,
            embeddedFieldId,
            value);

}
