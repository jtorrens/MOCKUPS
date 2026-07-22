using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorDictionaryFieldServices
{
    private readonly DictionaryFieldContextDataSource _contextData;
    private readonly EditorPathBrowser _pathBrowser;
    private readonly EditorDomainDialogService _domainDialogs;
    private readonly Func<string?> _selectedThemeId;
    private readonly Action<string, string> _setRuntimeTestValue;
    private readonly EditorSessionUiState _structuredCollectionUiState = new();

    public EditorDictionaryFieldServices(
        SpikeDatabase database,
        EditorPathBrowser pathBrowser,
        EditorDomainDialogService domainDialogs,
        Func<string?> selectedThemeId,
        Action<string, string> setRuntimeTestValue)
    {
        _contextData = new DictionaryFieldContextDataSource(database);
        _pathBrowser = pathBrowser;
        _domainDialogs = domainDialogs;
        _selectedThemeId = selectedThemeId;
        _setRuntimeTestValue = setRuntimeTestValue;
    }

    public DictionaryFieldServices ForNode(
        ProjectTreeNode node,
        Func<string, string> getFieldValue,
        Func<string, Task>? openComponentVariantReference = null,
        Func<string, Task>? openEmbeddedComponent = null,
        Func<FieldDefinition, ComponentInputBindingDefinition, Task>? openComponentInputBinding = null,
        Action<EditorEmbeddedContext>? openRuntimeComponentOverrides = null)
    {
        var projectId = ProjectAncestor(node).Id;
        string IconThemeId()
        {
            return _contextData.IconThemeId(node, _selectedThemeId());
        }
        JsonObject ThemeTokens()
        {
            return _contextData.ThemeTokens(node, _selectedThemeId());
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
        Task OpenRuntimeOverrides(string variantReference, JsonObject overrides, Action<JsonObject> changed)
        {
            if (openRuntimeComponentOverrides is null) return Task.CompletedTask;
            var selected = _contextData.ComponentVariantSelection(variantReference);
            openRuntimeComponentOverrides(new EditorEmbeddedContext(
                node,
                [],
                new RuntimeComponentOverrideSource(
                    selected.ProjectId,
                    variantReference,
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
            CreateIconPreview: (token) => SvgIconPreview.CreateIconTokenPreview(
                token,
                18,
                (singleToken) => _contextData.IconTokenAssetPath(IconThemeId(), singleToken)),
            ResolveImagePath: _pathBrowser.ResolveImagePath,
            GetFieldValue: getFieldValue,
            GetPaletteColorOptions: () => _contextData.PaletteColorOptions(projectId),
            GetComponentVariantOptions: (componentType) => _contextData.ComponentVariantOptions(projectId, componentType),
            GetComponentVariantRuntimeInputs: _contextData.ComponentVariantRuntimeInputBindings,
            GetComponentVariantRuntimeValues: _contextData.ComponentVariantRuntimeValues,
            GetComponentVariantRuntimeCollections: _contextData.ComponentVariantRuntimeCollections,
            OpenComponentVariantReference: openComponentVariantReference,
            OpenEmbeddedComponent: openEmbeddedComponent,
            OpenComponentInputBinding: openComponentInputBinding,
            ResolveBehaviorTimingFrames: ResolveBehaviorTimingFrames,
            ConfirmStopRuntimeInputForwarding: _domainDialogs.ConfirmStopRuntimeInputForwarding,
            OpenRuntimeComponentOverrides: openRuntimeComponentOverrides is null ? null : OpenRuntimeOverrides,
            ConfirmStructuredCollectionItemDelete: _domainDialogs.ConfirmRuntimeCollectionItemDelete,
            ConfirmDiscardForwardedRuntimeInputs: _domainDialogs.ConfirmDiscardForwardedRuntimeInputs,
            SetRuntimeTestValue: _setRuntimeTestValue,
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
