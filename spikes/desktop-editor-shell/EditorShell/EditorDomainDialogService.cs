using Avalonia.Controls;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorDomainDialogService
{
    private readonly Window _owner;
    private readonly SpikeDatabase _database;
    private readonly Func<bool> _isDark;
    private readonly Func<string, string, Task> _showInfo;
    private readonly Func<Task<string?>> _browseSvgFile;
    private readonly Action<ProjectTreeNode> _reloadAndSelect;

    public EditorDomainDialogService(
        Window owner,
        SpikeDatabase database,
        Func<bool> isDark,
        Func<string, string, Task> showInfo,
        Func<Task<string?>> browseSvgFile,
        Action<ProjectTreeNode> reloadAndSelect)
    {
        _owner = owner;
        _database = database;
        _isDark = isDark;
        _showInfo = showInfo;
        _browseSvgFile = browseSvgFile;
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

    public Task<SpikeDatabase.ShotModuleChoice?> SelectModuleForShot(string shotId)
    {
        return new ShotModulePickerDialog(_owner).Show(_database.GetAvailableShotModules(shotId));
    }

    public Task ShowIconThemeSearch(ProjectTreeNode node)
    {
        return new IconThemeSearchDialog(_owner, _database, _showInfo, _reloadAndSelect).Show(node);
    }

    public Task ShowIconThemeSvgReplace(ProjectTreeNode node, string token)
    {
        return new IconThemeSvgReplaceDialog(_owner, _database, _browseSvgFile, _reloadAndSelect).Show(node, token);
    }

    public Task<string?> ShowIconTokenPicker(string iconThemeId, string currentValue, bool allowMultiple)
    {
        return new IconTokenPickerDialog(_owner, _database).Show(iconThemeId, currentValue, allowMultiple);
    }

    public Task<string?> ShowThemeTokenPicker(string projectId, string currentValue, IReadOnlyList<FieldOption>? allowedOptions)
    {
        return new ThemeTokenPickerDialog(_owner, _database).Show(projectId, currentValue, allowedOptions);
    }
}
