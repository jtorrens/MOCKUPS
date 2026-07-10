using Avalonia.Platform.Storage;
using Mockups.DesktopEditorShell.Common;
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
        return valueKind switch
        {
            ValueKind.ImageFilePath => BrowseImageFile(_storageProvider, currentPath, SelectedProjectMediaRoot()),
            ValueKind.MediaFilePath => BrowseMediaFile(_storageProvider, currentPath, SelectedProjectMediaRoot()),
            _ => BrowseDirectory(currentPath),
        };
    }

    public async Task<string?> BrowseSvgFile()
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Select SVG icon",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("SVG")
                {
                    Patterns = ["*.svg"],
                    AppleUniformTypeIdentifiers = ["public.svg-image"],
                    MimeTypes = ["image/svg+xml"],
                },
            ],
        };

        var mediaRoot = SelectedProjectMediaRoot();
        if (!string.IsNullOrWhiteSpace(mediaRoot))
        {
            var fullMediaRoot = ProjectPathService.ResolveProjectPath(mediaRoot);
            if (Directory.Exists(fullMediaRoot))
            {
                options.SuggestedStartLocation = await _storageProvider.TryGetFolderFromPathAsync(fullMediaRoot);
            }
        }

        var files = await _storageProvider.OpenFilePickerAsync(options);
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public string? ResolveImagePath(string path)
    {
        return ProjectPathService.ResolveLocalPath(path, SelectedProjectMediaRoot());
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
            var fullPath = ProjectPathService.ResolveProjectPath(currentPath);
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

    public static async Task<string?> BrowseImageFile(
        IStorageProvider storageProvider,
        string currentPath,
        string? mediaRoot)
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
                    ? Path.GetFullPath(Path.Combine(ProjectPathService.ResolveProjectPath(mediaRoot), currentPath))
                    : ProjectPathService.ResolveProjectPath(currentPath);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(parent);
            }
        }

        var files = await storageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return null;

        var selectedPath = files[0].Path.LocalPath;
        return ProjectPathService.RelativePathIfInsideMediaRoot(selectedPath, mediaRoot);
    }

    public static async Task<string?> BrowseMediaFile(
        IStorageProvider storageProvider,
        string currentPath,
        string? mediaRoot)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Select media file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Media")
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.heic", "*.mp4", "*.mov", "*.m4v", "*.webm"],
                    AppleUniformTypeIdentifiers = ["public.image", "public.movie", "public.video"],
                    MimeTypes = ["image/png", "image/jpeg", "image/webp", "video/mp4", "video/quicktime", "video/webm"],
                },
            ],
        };

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var fullPath = Path.IsPathFullyQualified(currentPath)
                ? currentPath
                : !string.IsNullOrWhiteSpace(mediaRoot)
                    ? Path.GetFullPath(Path.Combine(ProjectPathService.ResolveProjectPath(mediaRoot), currentPath))
                    : ProjectPathService.ResolveProjectPath(currentPath);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(parent);
            }
        }

        var files = await storageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return null;

        var selectedPath = files[0].Path.LocalPath;
        return ProjectPathService.RelativePathIfInsideMediaRoot(selectedPath, mediaRoot) ?? selectedPath;
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
