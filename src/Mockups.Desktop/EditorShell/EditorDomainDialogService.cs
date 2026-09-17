using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorDomainDialogService
{
    private readonly Window _owner;
    private readonly IEditorChildStore _children;
    private readonly IModuleInstanceCollectionStore _moduleInstances;
    private readonly IIconThemeAssetStore _iconThemes;
    private readonly IThemeTokenQuery _themeTokens;
    private readonly EditorOperationCoordinator _operations;
    private readonly Func<bool> _isDark;
    private readonly Func<string, string, Task> _showInfo;
    private readonly Func<Task<string?>> _browseSvgFile;
    private readonly Func<string, ValueKind, Task<string?>> _browsePath;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;

    public EditorDomainDialogService(
        Window owner,
        IEditorChildStore children,
        IModuleInstanceCollectionStore moduleInstances,
        IIconThemeAssetStore iconThemes,
        IThemeTokenQuery themeTokens,
        EditorOperationCoordinator operations,
        Func<bool> isDark,
        Func<string, string, Task> showInfo,
        Func<Task<string?>> browseSvgFile,
        Func<string, ValueKind, Task<string?>> browsePath,
        Action<ProjectTreeNode> reloadAndSelect)
    {
        _owner = owner;
        _children = children;
        _moduleInstances = moduleInstances;
        _iconThemes = iconThemes;
        _themeTokens = themeTokens;
        _operations = operations;
        _isDark = isDark;
        _showInfo = showInfo;
        _browseSvgFile = browseSvgFile;
        _browsePath = browsePath;
        _reloadAndSelect = reloadAndSelect;
    }

    public Task<bool> ConfirmIconTokenDelete(string token)
    {
        return new EditorDialogService(_owner, _isDark()).ConfirmIconTokenDelete(token);
    }

    public Task<bool> ConfirmTestValueDefaults(string destination, IReadOnlyList<string> fields)
    {
        var fieldList = string.Join(", ", fields);
        return new EditorDialogService(_owner, _isDark()).ConfirmAction(
            "Save default values",
            $"Save the test values to \"{destination}\"?",
            $"The following fields will be updated: {fieldList}.",
            "Save default values",
            width: 500,
            height: 240);
    }

    public Task<bool> ConfirmRuntimeCollectionItemDelete(string label)
    {
        return new EditorDialogService(_owner, _isDark()).ConfirmCollectionItemDelete(label);
    }

    public Task<bool> ConfirmAnimationDisable(string fieldLabel)
    {
        return new EditorDialogService(_owner, _isDark()).ConfirmAnimationDisable(fieldLabel);
    }

    public Task<RecordCreationDraft?> ShowRuntimeCreation(
        RecordCreationDefinition definition)
    {
        return new RecordCreationDialog(
            _owner,
            new DictionaryFieldServices(BrowsePath: _browsePath)).Show(definition);
    }

    public Task<bool> ConfirmStopRuntimeInputForwarding(string fieldLabel)
    {
        return new EditorDialogService(_owner, _isDark()).ConfirmAction(
            "Keep input in Variant",
            $"Stop exposing \"{fieldLabel}\" to the parent runtime?",
            "Its current value will remain fixed in this Variant.",
            "Keep as Variant value",
            width: 500,
            height: 240);
    }

    public Task<bool> ConfirmDiscardForwardedRuntimeInputs(
        string action,
        IReadOnlyList<string> fieldLabels)
    {
        var fields = string.Join(", ", fieldLabels.Select((label) => $"\"{label}\""));
        return new EditorDialogService(_owner, _isDark()).ConfirmAction(
            "Remove forwarded runtime inputs",
            $"{action}?",
            $"These forwarded fields will be removed from Runtime Inputs: {fields}.",
            "Accept",
            width: 520,
            height: 250);
    }

    public Task<bool> ConfirmUsedRuntimeContractReplacement(
        string variantName)
    {
        return new EditorDialogService(_owner, _isDark()).ConfirmAction(
            "Replace Screen Runtime payloads",
            $"Change the component used by \"{variantName}\"?",
            "This Module Variant is used by one or more Screens. Their previous Runtime payload and animation tracks will be removed and replaced with fresh values for the new contract.",
            "Replace and reset Screens",
            width: 560,
            height: 270);
    }

    public async Task<RecordCreationDraft?> DefineModuleInstanceForShot(string shotId)
    {
        var selection = await new ShotModulePickerDialog(
            _owner,
            _moduleInstances,
            _operations).Show(shotId);
        if (selection is null) return null;
        var selectionValues =
            EditorAddChildWorkflow.ModuleInstanceSelectionValues(selection);
        var shot = new ProjectTreeNode(
            ProjectTreeNodeKind.Shot,
            shotId,
            "Shot",
            "",
            ProjectTreeNode.DefaultRecordClassId(ProjectTreeNodeKind.Shot));
        var definition = await _operations.ExecuteWithActivityAsync(
            "Preparing Screen Runtime Inputs…",
            () => _children.PrepareRecordCreation(
                shot,
                "moduleInstance",
                selectionValues));
        var runtimeValues = definition.RequiresConfirmation
            ? await new RecordCreationDialog(
                _owner,
                new DictionaryFieldServices(BrowsePath: _browsePath)).Show(definition)
            : new RecordCreationDraft(
                definition.Id,
                definition.Fields.ToDictionary(
                    (field) => field.Definition.Id,
                    (field) => field.Value,
                    StringComparer.Ordinal));
        return runtimeValues is null
            ? null
            : runtimeValues with
            {
                SelectionValues = selectionValues,
                OperationId = "moduleInstance",
            };
    }

    public Task<bool> ConfirmModuleInstanceDelete(ProjectTreeNode node)
    {
        return new EditorDialogService(_owner, _isDark()).ConfirmDelete(node);
    }

    public Task ShowIconThemeSearch(ProjectTreeNode node)
    {
        return new IconThemeSearchDialog(
            _owner,
            _iconThemes,
            _operations,
            _showInfo,
            _reloadAndSelect).Show(node);
    }

    public Task ShowIconThemeSvgReplace(ProjectTreeNode node, string token)
    {
        return new IconThemeSvgReplaceDialog(
            _owner,
            _iconThemes,
            _operations,
            _browseSvgFile,
            _reloadAndSelect).Show(node, token);
    }

    public Task<string?> ShowIconTokenPicker(string iconThemeId, string currentValue, bool allowMultiple)
    {
        return new IconTokenPickerDialog(
            _owner,
            _iconThemes,
            _operations).Show(iconThemeId, currentValue, allowMultiple);
    }

    public Task<string?> ShowThemeTokenPicker(string projectId, string currentValue, IReadOnlyList<FieldOption>? allowedOptions)
    {
        return new ThemeTokenPickerDialog(
            _owner,
            _themeTokens,
            _operations).Show(projectId, currentValue, allowedOptions);
    }
}
