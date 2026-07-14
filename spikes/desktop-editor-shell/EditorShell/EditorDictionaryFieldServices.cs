using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorDictionaryFieldServices
{
    private readonly SpikeDatabase _database;
    private readonly EditorPathBrowser _pathBrowser;
    private readonly EditorDomainDialogService _domainDialogs;
    private readonly Func<string?> _selectedThemeId;
    private readonly EditorSessionUiState _structuredCollectionUiState = new();

    public EditorDictionaryFieldServices(
        SpikeDatabase database,
        EditorPathBrowser pathBrowser,
        EditorDomainDialogService domainDialogs,
        Func<string?> selectedThemeId)
    {
        _database = database;
        _pathBrowser = pathBrowser;
        _domainDialogs = domainDialogs;
        _selectedThemeId = selectedThemeId;
    }

    public DictionaryFieldServices ForNode(
        ProjectTreeNode node,
        Func<string, string> getFieldValue,
        Func<string, Task>? openComponentPresetReference = null,
        Func<string, Task>? openEmbeddedComponent = null,
        Func<FieldDefinition, ComponentInputBindingDefinition, Task>? openComponentInputBinding = null,
        Action<EditorEmbeddedContext>? openRuntimeComponentOverrides = null)
    {
        var projectId = ProjectAncestor(node).Id;
        string IconThemeId()
        {
            var effectiveThemeId = DesignPreviewPayloadFactory.ResolveThemeId(_database, node, _selectedThemeId());
            return string.IsNullOrWhiteSpace(effectiveThemeId)
                ? ""
                : _database.GetThemeSettings(effectiveThemeId).IconThemeId;
        }
        JsonObject ThemeTokens()
        {
            if (node.Kind == ProjectTreeNodeKind.ModuleInstance)
            {
                return DesignPreviewTestValues.Parse(_database.GetModuleInstanceThemeTokensJson(node.Id));
            }
            var effectiveThemeId = DesignPreviewPayloadFactory.ResolveThemeId(_database, node, _selectedThemeId());
            return string.IsNullOrWhiteSpace(effectiveThemeId)
                ? new JsonObject()
                : DesignPreviewTestValues.Parse(_database.GetThemeSettings(effectiveThemeId).TokensJson);
        }
        int? ResolveBehaviorTimingFrames(FieldDefinition definition, string json)
        {
            if (definition.BehaviorTiming is not { } timing) return null;
            try
            {
                var value = BehaviorTimingValue.Parse(json);
                if (value.Mode == "fixed") return value.FixedFrames;
                return BehaviorTimingResolver.ResolveNaturalFrames(
                    getFieldValue(timing.SourceFieldId),
                    timing.Unit,
                    timing.BaseFramesPerUnit,
                    value.PaceToken,
                    ThemeTokens());
            }
            catch
            {
                return null;
            }
        }
        Task OpenRuntimeOverrides(string presetReference, JsonObject overrides, Action<JsonObject> changed)
        {
            if (openRuntimeComponentOverrides is null) return Task.CompletedTask;
            var selected = _database.GetComponentPresetSelectionSettings(presetReference);
            openRuntimeComponentOverrides(new EditorEmbeddedContext(
                node,
                [],
                new RuntimeComponentOverrideSource(
                    selected.ProjectId,
                    presetReference,
                    selected.ComponentType,
                    selected.RecordClassId,
                    selected.ConfigJson,
                    overrides,
                    changed)));
            return Task.CompletedTask;
        }
        return new DictionaryFieldServices(
            BrowsePath: _pathBrowser.BrowsePath,
            ShowIconTokenPicker: (currentValue, allowMultiple) => _domainDialogs.ShowIconTokenPicker(IconThemeId(), currentValue, allowMultiple),
            ShowThemeTokenPicker: (currentValue, allowedOptions) => _domainDialogs.ShowThemeTokenPicker(projectId, currentValue, allowedOptions),
            CreateIconPreview: (token) => SvgIconPreview.CreateIconTokenPreview(_database, IconThemeId(), token, 18),
            ResolveImagePath: _pathBrowser.ResolveImagePath,
            GetFieldValue: getFieldValue,
            GetPaletteColorOptions: () => _database.GetPaletteColorOptions(projectId),
            GetComponentPresetOptions: (componentType) => _database.GetComponentPresetReferenceOptionsByType(projectId, componentType),
            GetComponentPresetRuntimeInputs: _database.GetComponentPresetRuntimeInputBindings,
            GetComponentPresetRuntimeValues: _database.GetComponentPresetRuntimeInputs,
            GetComponentPresetRuntimeCollections: _database.GetComponentPresetRuntimeCollections,
            OpenComponentPresetReference: openComponentPresetReference,
            OpenEmbeddedComponent: openEmbeddedComponent,
            OpenComponentInputBinding: openComponentInputBinding,
            ResolveBehaviorTimingFrames: ResolveBehaviorTimingFrames,
            ConfirmStopRuntimeInputForwarding: _domainDialogs.ConfirmStopRuntimeInputForwarding,
            OpenRuntimeComponentOverrides: openRuntimeComponentOverrides is null ? null : OpenRuntimeOverrides,
            ConfirmStructuredCollectionItemDelete: _domainDialogs.ConfirmRuntimeCollectionItemDelete,
            StructuredCollectionUiState: _structuredCollectionUiState);
    }

    private static ProjectTreeNode ProjectAncestor(ProjectTreeNode node)
    {
        var current = node;
        while (current.Kind != ProjectTreeNodeKind.Project)
        {
            current = current.Parent ?? throw new InvalidOperationException($"{node.Kind} has no project ancestor.");
        }

        return current;
    }
}
