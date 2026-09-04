using Mockups.DesktopEditorShell.Data;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorDictionaryFieldServices
{
    private readonly DictionaryFieldContextDataSource _contextData;
    private readonly EditorPathBrowser _pathBrowser;
    private readonly EditorDomainDialogService _domainDialogs;
    private readonly RuntimeInputOptionsDataSource _runtimeInputOptions;
    private readonly EditorDictionaryContextPreparer _contextPreparer;
    private readonly EditorOperationCoordinator _operations;
    private readonly Func<string?> _selectedThemeId;
    private readonly Action<string, string> _setRuntimeTestValue;
    private readonly EditorSessionUiState _structuredCollectionUiState = new();

    public EditorDictionaryFieldServices(
        IDictionaryFieldContextRepository database,
        IPreviewInputRepository preview,
        IModuleInstanceTimelineStore timeline,
        IModuleInstanceThemeTokenQuery moduleInstanceThemes,
        IActorPreviewRepository actors,
        IProjectPathResolver projectPaths,
        EditorPathBrowser pathBrowser,
        EditorDomainDialogService domainDialogs,
        EditorOperationCoordinator operations,
        Func<string?> selectedThemeId,
        Action<string, string> setRuntimeTestValue)
    {
        _contextData = new DictionaryFieldContextDataSource(
            database,
            preview,
            timeline,
            moduleInstanceThemes,
            actors,
            projectPaths);
        _pathBrowser = pathBrowser;
        _domainDialogs = domainDialogs;
        _operations = operations;
        _runtimeInputOptions =
            new RuntimeInputOptionsDataSource(database, actors);
        _contextPreparer = new EditorDictionaryContextPreparer(
            _contextData,
            _runtimeInputOptions);
        _selectedThemeId = selectedThemeId;
        _setRuntimeTestValue = setRuntimeTestValue;
    }

    public string? CaptureSelectedThemeId() => _selectedThemeId();

    public EditorDictionaryContextSnapshot PrepareContext(
        ProjectTreeNode node,
        string? selectedThemeId,
        IReadOnlyDictionary<string, FieldValue> fields,
        CancellationToken cancellationToken) =>
        _contextPreparer.Prepare(
            node,
            selectedThemeId,
            fields,
            cancellationToken);

    public EditorDictionaryContextSnapshot PrepareContext(
        ProjectTreeNode node,
        string? selectedThemeId,
        IEnumerable<IReadOnlyDictionary<string, FieldValue>> fieldSets,
        CancellationToken cancellationToken) =>
        _contextPreparer.Prepare(
            node,
            selectedThemeId,
            fieldSets,
            cancellationToken);

    public EditorDictionaryContextSnapshot PrepareRuntimeContext(
        ProjectTreeNode node,
        string? selectedThemeId,
        RuntimeInputSurface surface,
        CancellationToken cancellationToken) =>
        _contextPreparer.PrepareRuntimeContext(
            node,
            selectedThemeId,
            surface,
            cancellationToken);

    public DictionaryFieldServices ForPreparedNode(
        ProjectTreeNode node,
        EditorDictionaryContextSnapshot context,
        Func<string, string> getFieldValue,
        Func<string, Task>? openComponentVariantReference = null,
        Func<string, Task>? openEmbeddedComponent = null,
        Func<FieldDefinition, ComponentInputBindingDefinition, Task>? openComponentInputBinding = null,
        Action<EditorEmbeddedContext>? openRuntimeComponentOverrides = null,
        Func<FieldDefinition, string, Task>?
            openRecordReferenceOverrides = null,
        Func<string, Task>?
            restoreEmbeddedComponentOverrides = null,
        Func<FieldDefinition, string, Task>?
            restoreRecordReferenceOverrides = null)
    {
        int ResolveBehaviorTimingFrames(
            FieldDefinition definition,
            string json)
        {
            if (definition.BehaviorTiming is not { } timing)
            {
                throw new InvalidOperationException(
                    $"Behavior Timing field '{definition.Id}' is missing its natural timing definition.");
            }
            var value = BehaviorTimingValue.Parse(json);
            if (value.Mode == "fixed") return value.FixedFrames;
            return BehaviorTimingResolver.ResolveNaturalFrames(
                getFieldValue(timing.SourceFieldId),
                timing.Unit,
                timing.BaseFramesPerUnit,
                value.PaceToken,
                context.ThemeTokens());
        }
        async Task OpenRuntimeOverrides(
            string variantReference,
            JsonObject overrides,
            Func<JsonObject, Task> changed)
        {
            if (openRuntimeComponentOverrides is null)
            {
                return;
            }
            var selected = context.TryVariantSelection(
                variantReference,
                out var preparedSelection)
                ? preparedSelection
                : await _operations.ExecuteAsync(
                    () => _contextData
                        .ComponentVariantSelection(
                            variantReference));
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
        }
        return new DictionaryFieldServices(
            BrowsePath: _pathBrowser.BrowsePath,
            ShowIconTokenPicker: (currentValue, allowMultiple) =>
                _domainDialogs.ShowIconTokenPicker(
                    context.IconThemeId,
                    currentValue,
                    allowMultiple),
            ShowThemeTokenPicker: (currentValue, allowedOptions) =>
                _domainDialogs.ShowThemeTokenPicker(
                    context.ProjectId,
                    currentValue,
                    allowedOptions),
            CreateIconPreview: (token) =>
                SvgIconPreview.CreateIconTokenPreview(
                    token,
                    18,
                    context.IconAssetPath),
            ResolveImagePath: _pathBrowser.ResolveImagePath,
            GetFieldValue: getFieldValue,
            GetPaletteColorOptions: () =>
                context.PaletteColorOptions,
            GetRecordReferenceOptions: context.RecordOptions,
            GetComponentVariantOptions: context.VariantOptions,
            GetComponentVariantRuntimeInputs: context.RuntimeInputs,
            GetComponentVariantRuntimeValues: context.RuntimeValues,
            GetComponentVariantRuntimeCollections:
                context.RuntimeCollections,
            OpenComponentVariantReference:
                openComponentVariantReference,
            OpenEmbeddedComponent: openEmbeddedComponent,
            RestoreEmbeddedComponentOverrides:
                restoreEmbeddedComponentOverrides,
            OpenComponentInputBinding: openComponentInputBinding,
            ResolveBehaviorTimingFrames:
                ResolveBehaviorTimingFrames,
            ConfirmStopRuntimeInputForwarding:
                _domainDialogs.ConfirmStopRuntimeInputForwarding,
            OpenRuntimeComponentOverrides:
                openRuntimeComponentOverrides is null
                    ? null
                    : OpenRuntimeOverrides,
            OpenRecordReferenceOverrides:
                openRecordReferenceOverrides,
            RestoreRecordReferenceOverrides:
                restoreRecordReferenceOverrides,
            ConfirmStructuredCollectionItemDelete:
                _domainDialogs.ConfirmRuntimeCollectionItemDelete,
            ConfirmDiscardForwardedRuntimeInputs:
                _domainDialogs.ConfirmDiscardForwardedRuntimeInputs,
            SetRuntimeTestValue: _setRuntimeTestValue,
            StructuredCollectionUiState:
                _structuredCollectionUiState);
    }

    public DictionaryFieldServices ForNode(
        ProjectTreeNode node,
        Func<string, string> getFieldValue,
        Func<string, Task>? openComponentVariantReference = null,
        Func<string, Task>? openEmbeddedComponent = null,
        Func<FieldDefinition, ComponentInputBindingDefinition, Task>? openComponentInputBinding = null,
        Action<EditorEmbeddedContext>? openRuntimeComponentOverrides = null,
        Func<FieldDefinition, string, Task>?
            openRecordReferenceOverrides = null,
        Func<string, Task>?
            restoreEmbeddedComponentOverrides = null,
        Func<FieldDefinition, string, Task>?
            restoreRecordReferenceOverrides = null)
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
        int ResolveBehaviorTimingFrames(FieldDefinition definition, string json)
        {
            if (definition.BehaviorTiming is not { } timing)
            {
                throw new InvalidOperationException(
                    $"Behavior Timing field '{definition.Id}' is missing its natural timing definition.");
            }
            var value = BehaviorTimingValue.Parse(json);
            if (value.Mode == "fixed") return value.FixedFrames;
            return BehaviorTimingResolver.ResolveNaturalFrames(
                getFieldValue(timing.SourceFieldId),
                timing.Unit,
                timing.BaseFramesPerUnit,
                value.PaceToken,
                ThemeTokens());
        }
        Task OpenRuntimeOverrides(
            string variantReference,
            JsonObject overrides,
            Func<JsonObject, Task> changed)
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
            GetRecordReferenceOptions: (tableId, includeNone) =>
                _runtimeInputOptions.RecordReferenceOptions(projectId, tableId, includeNone),
            GetComponentVariantOptions: (componentType) => _contextData.ComponentVariantOptions(projectId, componentType),
            GetComponentVariantRuntimeInputs: _contextData.ComponentVariantRuntimeInputBindings,
            GetComponentVariantRuntimeValues: _contextData.ComponentVariantRuntimeValues,
            GetComponentVariantRuntimeCollections: _contextData.ComponentVariantRuntimeCollections,
            OpenComponentVariantReference: openComponentVariantReference,
            OpenEmbeddedComponent: openEmbeddedComponent,
            RestoreEmbeddedComponentOverrides:
                restoreEmbeddedComponentOverrides,
            OpenComponentInputBinding: openComponentInputBinding,
            ResolveBehaviorTimingFrames: ResolveBehaviorTimingFrames,
            ConfirmStopRuntimeInputForwarding: _domainDialogs.ConfirmStopRuntimeInputForwarding,
            OpenRuntimeComponentOverrides: openRuntimeComponentOverrides is null ? null : OpenRuntimeOverrides,
            OpenRecordReferenceOverrides:
                openRecordReferenceOverrides,
            RestoreRecordReferenceOverrides:
                restoreRecordReferenceOverrides,
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
