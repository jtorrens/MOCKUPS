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
    private readonly ComponentClassFieldValueService _componentFields;
    private readonly Func<string?> _selectedThemeId;
    private readonly Action<string, string> _setRuntimeTestValue;
    private readonly Action<ProjectTreeNode, string, string, bool> _setOwnerOverrideValue;
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
        ComponentClassFieldValueService componentFields,
        Func<string?> selectedThemeId,
        Action<string, string> setRuntimeTestValue,
        Action<ProjectTreeNode, string, string, bool> setOwnerOverrideValue)
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
        _componentFields = componentFields;
        _runtimeInputOptions =
            new RuntimeInputOptionsDataSource(database, actors);
        _contextPreparer = new EditorDictionaryContextPreparer(
            _contextData,
            _runtimeInputOptions);
        _selectedThemeId = selectedThemeId;
        _setRuntimeTestValue = setRuntimeTestValue;
        _setOwnerOverrideValue = setOwnerOverrideValue;
    }

    public void PublishTransientOverrideField(ProjectTreeNode node, string key, string value, bool isCollection) =>
        Avalonia.Threading.Dispatcher.UIThread.Invoke(() => _setOwnerOverrideValue(node, key, value, isCollection));

    public string? CaptureSelectedThemeId() => _selectedThemeId();

    public Task RestoreRuntimeOverridesAsync(EditorEmbeddedContext context) =>
        _componentFields.ClearEmbeddedComponentOverridesAsync(context);

    public RuntimeComponentOverrideSource RegisterRuntimeOverrides(
        ComponentOverrideFieldOwner owner, ComponentOverrideAddress address,
        string projectId, string reference, string type, string recordClassId) =>
        _componentFields.RegisterRuntimeOverrides(owner, address, projectId, reference, type, recordClassId);

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
            restoreRecordReferenceOverrides = null,
        ComponentOverrideFieldOwner? overrideOwner = null,
        Action? overridesRestored = null)
    {
        var projectId = ProjectAncestor(node).Id;
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
        async Task<EditorEmbeddedContext> OverrideContext(
            ComponentOverrideAddress address,
            string variantReference,
            FieldDefinition definition)
        {
            var selected = context.TryVariantSelection(
                variantReference,
                out var preparedSelection)
                ? preparedSelection
                : await _operations.ExecuteAsync(
                    () => _contextData
                        .ComponentVariantSelection(
                            projectId,
                            variantReference));
            var owner = overrideOwner
                ?? throw new InvalidOperationException("Override navigation requires its authoring owner.");
            return new EditorEmbeddedContext(
                node,
                [],
                _componentFields.RegisterRuntimeOverrides(
                    owner with
                    {
                        ThemeVariantReference = source => _contextData.ThemeComponentVariantReference(
                            node, Avalonia.Threading.Dispatcher.UIThread.Invoke(_selectedThemeId), source),
                    },
                    address with { ThemeSource = definition.ThemeComponentVariantSource },
                    selected.ProjectId,
                    variantReference,
                    selected.ComponentType,
                    selected.RecordClassId));
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
            GetThemeComponentVariantReference:
                context.ThemeComponentVariantReference,
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
                    : async (address, reference, definition) => openRuntimeComponentOverrides(
                        await OverrideContext(address, reference, definition)),
            RestoreRuntimeComponentOverrides: overrideOwner is null ? null
                : async (address, reference, definition) =>
                {
                    var target = await OverrideContext(address, reference, definition);
                    await _componentFields.ClearEmbeddedComponentOverridesAsync(target);
                    overridesRestored?.Invoke();
                },
            OpenRecordReferenceOverrides:
                openRecordReferenceOverrides,
            RestoreRecordReferenceOverrides:
                restoreRecordReferenceOverrides,
            ConfirmStructuredCollectionItemDelete:
                _domainDialogs.ConfirmRuntimeCollectionItemDelete,
            ConfirmDiscardForwardedRuntimeInputs:
                _domainDialogs.ConfirmDiscardForwardedRuntimeInputs,
            ConfirmUsedRuntimeContractReplacement:
                node.Kind == ProjectTreeNodeKind.ModuleVariant
                    && node.IsUsed
                    ? () => _domainDialogs
                        .ConfirmUsedRuntimeContractReplacement(node.Name)
                    : null,
            SetRuntimeTestValue: _setRuntimeTestValue,
            StructuredCollectionUiState:
                _structuredCollectionUiState);
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
