using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorAddChildWorkflow
{
    private readonly Window _owner;
    private readonly IEditorChildStore _database;
    private readonly IModuleInstanceCollectionStore _moduleInstances;
    private readonly IProjectPathResolver _projectPaths;
    private readonly EditorOperationCoordinator _operations;
    private readonly Func<string, string, Task> _showInfo;

    public EditorAddChildWorkflow(
        Window owner,
        IEditorChildStore database,
        IModuleInstanceCollectionStore moduleInstances,
        IProjectPathResolver projectPaths,
        EditorOperationCoordinator operations,
        Func<string, string, Task> showInfo)
    {
        _owner = owner;
        _database = database;
        _moduleInstances = moduleInstances;
        _projectPaths = projectPaths;
        _operations = operations;
        _showInfo = showInfo;
    }

    public async Task<ProjectTreeNode?> TryAdd(ProjectTreeNode parent)
    {
        if (!EditorAddOperationCatalog.TryGet(parent.Kind, out var operation)) return null;
        return operation.Kind switch
        {
            EditorAddOperationKind.CreateRecord => await CreateRecord(parent, operation.CreationId),
            EditorAddOperationKind.ImportDevice => await ImportDevice(parent),
            EditorAddOperationKind.ImportProductionFont => await ImportProductionFont(parent),
            EditorAddOperationKind.RefreshIconThemes => await RefreshAndReturn(parent),
            EditorAddOperationKind.SelectModuleInstance => await SelectModuleInstance(parent),
            _ => throw new InvalidOperationException(
                $"Add operation '{operation.Id}' has unsupported kind '{operation.Kind}'."),
        };
    }

    private async Task<ProjectTreeNode?> CreateRecord(ProjectTreeNode parent, string creationId)
    {
        try
        {
            var definition = await _operations.ExecuteAsync(
                () => _database.PrepareRecordCreation(parent, creationId));
            var draft = definition.RequiresConfirmation
                ? await new RecordCreationDialog(_owner).Show(definition)
                : new RecordCreationDraft(
                    definition.Id,
                    definition.Fields.ToDictionary(
                        (field) => field.Definition.Id,
                        (field) => field.Value,
                        StringComparer.Ordinal));
            return draft is null
                ? null
                : await _operations.ExecuteAsync(
                    () => _database.CreateRecord(parent, draft));
        }
        catch (Exception exception)
        {
            await _showInfo("Record creation failed", exception.Message);
            return null;
        }
    }

    private async Task<ProjectTreeNode?> SelectModuleInstance(ProjectTreeNode shot)
    {
        var draft = await new ShotModulePickerDialog(
            _owner, _moduleInstances, _operations).Show(shot.Id);
        return draft is null
            ? null
            : await _operations.ExecuteAsync(
                () => _moduleInstances.AddModuleInstance(shot, draft));
    }

    private async Task<ProjectTreeNode> RefreshAndReturn(ProjectTreeNode parent)
    {
        await RefreshIconThemes(parent);
        return parent;
    }

    private async Task<ProjectTreeNode?> ImportDevice(ProjectTreeNode devicesRoot)
    {
        try
        {
            var dialog = new DeviceImportDialog(_owner, new LabsViewportsDeviceCatalogProvider());
            var result = await dialog.ShowAsync();
            if (result is null) return null;
            if (result.CreateBlank)
            {
                var definition = await _operations.ExecuteAsync(
                    () => _database.PrepareRecordCreation(devicesRoot, "device"));
                return await _operations.ExecuteAsync(
                    () => _database.CreateRecord(
                        devicesRoot,
                        new RecordCreationDraft(
                            definition.Id,
                            new Dictionary<string, string>(StringComparer.Ordinal))));
            }
            return result.Draft is null
                ? null
                : await _operations.ExecuteAsync(
                    () => _database.AddImportedDevice(
                        devicesRoot,
                        result.Draft));
        }
        catch (Exception exception)
        {
            await _showInfo("Device import failed", exception.Message);
            return null;
        }
    }

    private async Task RefreshIconThemes(ProjectTreeNode parent)
    {
        try
        {
            var result = await _operations.ExecuteAsync(
                () => _database.RefreshIconThemeSets(parent));
            await _showInfo("Refresh complete", $"Refreshed {result.CommonTokenCount} common token(s) across {result.ThemeCount} icon set(s). Omitted {result.OmittedTokenCount} token(s) not present in every set.");
        }
        catch (Exception exception)
        {
            await _showInfo("Refresh failed", exception.Message);
        }
    }

    private async Task<ProjectTreeNode?> ImportProductionFont(ProjectTreeNode fontsRoot)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Import production font family",
            AllowMultiple = true,
            FileTypeFilter =
            [
                new FilePickerFileType("Font files")
                {
                    Patterns = ["*.ttf", "*.otf", "*.ttc", "*.woff", "*.woff2"],
                    AppleUniformTypeIdentifiers = ["public.font"],
                },
            ],
        };

        var project = ProjectAncestor(fontsRoot);
        var mediaRoot = await _operations.ExecuteAsync(
            () => _database.GetProjectSettings(project.Id).MediaRoot);
        var fullMediaRoot = ResolveProjectMediaRoot(mediaRoot);
        if (!string.IsNullOrWhiteSpace(fullMediaRoot) && Directory.Exists(fullMediaRoot))
        {
            options.SuggestedStartLocation = await _owner.StorageProvider.TryGetFolderFromPathAsync(fullMediaRoot);
        }

        var files = await _owner.StorageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return null;

        try
        {
            return await _operations.ExecuteAsync(
                () => _database.ImportProductionFont(
                    fontsRoot,
                    files.Select((file) => file.Path.LocalPath).ToList()));
        }
        catch (Exception exception)
        {
            await _showInfo("Import font failed", exception.Message);
            return null;
        }
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

    private string ResolveProjectMediaRoot(string mediaRoot)
    {
        return _projectPaths.ResolveProjectPath(mediaRoot);
    }
}
