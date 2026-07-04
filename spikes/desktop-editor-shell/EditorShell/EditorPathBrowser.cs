using Avalonia.Platform.Storage;
using Mockups.DesktopEditorShell.Data;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorPathBrowser
{
    private readonly IStorageProvider _storageProvider;
    private readonly SpikeDatabase _database;
    private readonly Func<ProjectTreeNode?> _selectedNode;

    public EditorPathBrowser(
        IStorageProvider storageProvider,
        SpikeDatabase database,
        Func<ProjectTreeNode?> selectedNode)
    {
        _storageProvider = storageProvider;
        _database = database;
        _selectedNode = selectedNode;
    }

    public Task<string?> BrowsePath(string currentPath, ValueKind valueKind)
    {
        return valueKind == ValueKind.ImageFilePath
            ? BrowseImageFile(currentPath, SelectedProjectMediaRoot())
            : BrowseDirectory(currentPath);
    }

    public string? ResolveImagePath(string path)
    {
        return MediaPathService.ResolveLocalPath(path, SelectedProjectMediaRoot());
    }

    private async Task<string?> BrowseDirectory(string currentPath)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = "Select media root",
            AllowMultiple = false,
        };

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var fullPath = Path.IsPathFullyQualified(currentPath)
                ? currentPath
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", currentPath));
            if (Directory.Exists(fullPath))
            {
                options.SuggestedStartLocation = await _storageProvider.TryGetFolderFromPathAsync(fullPath);
            }
        }

        var folders = await _storageProvider.OpenFolderPickerAsync(options);
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    private string? SelectedProjectMediaRoot()
    {
        var selectedNode = _selectedNode();
        if (selectedNode is null) return null;

        var project = ProjectAncestor(selectedNode);
        return _database.GetProjectSettings(project.Id).MediaRoot;
    }

    private async Task<string?> BrowseImageFile(string currentPath, string? mediaRoot)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Select avatar image",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Images")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.heic"],
                    AppleUniformTypeIdentifiers = ["public.image"],
                    MimeTypes = ["image/png", "image/jpeg", "image/webp"],
                },
            ],
        };

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var fullPath = Path.IsPathFullyQualified(currentPath)
                ? currentPath
                : !string.IsNullOrWhiteSpace(mediaRoot)
                    ? Path.GetFullPath(Path.Combine(mediaRoot, currentPath))
                    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", currentPath));
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                options.SuggestedStartLocation = await _storageProvider.TryGetFolderFromPathAsync(parent);
            }
        }

        var files = await _storageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return null;

        var selectedPath = files[0].Path.LocalPath;
        return MediaPathService.RelativePathIfInsideMediaRoot(selectedPath, mediaRoot);
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
