using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record DictionaryComponentVariantSelectionSource(
    string ProjectId,
    string ComponentType,
    string RecordClassId,
    string ConfigJson);

internal sealed class DictionaryFieldContextDataSource
{
    private readonly SpikeDatabase _database;
    private readonly DesignPreviewPayloadDataSource _previewPayloadData;

    public DictionaryFieldContextDataSource(SpikeDatabase database)
    {
        _database = database;
        _previewPayloadData = new DesignPreviewPayloadDataSource(database);
    }

    public string IconThemeId(ProjectTreeNode node, string? selectedThemeId)
    {
        var themeId = _previewPayloadData.ResolveThemeId(node, selectedThemeId);
        return string.IsNullOrWhiteSpace(themeId)
            ? ""
            : _database.GetThemeSettings(themeId).IconThemeId;
    }

    public JsonObject ThemeTokens(ProjectTreeNode node, string? selectedThemeId)
    {
        if (node.Kind == ProjectTreeNodeKind.ModuleInstance)
        {
            return DesignPreviewTestValues.Parse(_database.GetModuleInstanceThemeTokensJson(node.Id));
        }

        var themeId = _previewPayloadData.ResolveThemeId(node, selectedThemeId);
        return string.IsNullOrWhiteSpace(themeId)
            ? new JsonObject()
            : DesignPreviewTestValues.Parse(_database.GetThemeSettings(themeId).TokensJson);
    }

    public string? IconTokenAssetPath(string iconThemeId, string token)
    {
        if (string.IsNullOrWhiteSpace(iconThemeId) || string.IsNullOrWhiteSpace(token)) return null;
        var icon = _database.GetIconThemeTokens(iconThemeId)
            .FirstOrDefault((candidate) => candidate.Token == token);
        return icon is null ? null : _database.ResolveIconThemeAssetPath(iconThemeId, icon.File);
    }

    public IReadOnlyList<FieldOption> PaletteColorOptions(string projectId)
    {
        return _database.GetPaletteColorOptions(projectId);
    }

    public IReadOnlyList<FieldOption> ComponentVariantOptions(string projectId, string componentType)
    {
        return _database.GetComponentVariantReferenceOptionsByType(projectId, componentType);
    }

    public IReadOnlyList<ComponentInputBindingDefinition> ComponentVariantRuntimeInputBindings(
        string variantReference)
    {
        return _database.GetComponentVariantRuntimeInputBindings(variantReference);
    }

    public JsonObject ComponentVariantRuntimeValues(string variantReference)
    {
        return _database.GetComponentVariantRuntimeInputs(variantReference);
    }

    public IReadOnlyList<RuntimeInputCollectionDefinition> ComponentVariantRuntimeCollections(
        string variantReference)
    {
        return _database.GetComponentVariantRuntimeCollections(variantReference);
    }

    public DictionaryComponentVariantSelectionSource ComponentVariantSelection(string variantReference)
    {
        var selected = _database.GetComponentVariantSelectionSettings(variantReference);
        return new DictionaryComponentVariantSelectionSource(
            selected.ProjectId,
            selected.ComponentType,
            selected.RecordClassId,
            selected.ConfigJson);
    }
}
