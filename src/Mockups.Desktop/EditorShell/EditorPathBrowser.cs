using Avalonia.Platform.Storage;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorPathBrowser
{
    private readonly IStorageProvider _storageProvider;
    private readonly EditorPresentationContextDataSource _contextData;
    private readonly IProjectPathResolver _projectPaths;
    private readonly Func<ProjectTreeNode?> _selectedNode;
    private readonly Func<string, string, Task> _showInfo;

    public EditorPathBrowser(
        IStorageProvider storageProvider,
        IEditorPresentationContextRepository database,
        IProjectPathResolver projectPaths,
        Func<ProjectTreeNode?> selectedNode,
        Func<string, string, Task> showInfo)
    {
        _storageProvider = storageProvider;
        _contextData = new EditorPresentationContextDataSource(database);
        _projectPaths = projectPaths;
        _selectedNode = selectedNode;
        _showInfo = showInfo;
    }

    public async Task<string?> BrowsePath(string currentPath, ValueKind valueKind)
    {
        try
        {
            return await (valueKind switch
            {
                ValueKind.ImageFilePath => BrowseImageFile(
                    _storageProvider,
                    _projectPaths,
                    currentPath,
                    SelectedProjectMediaRoot()),
                ValueKind.MediaFilePath => BrowseMediaFile(
                    _storageProvider,
                    _projectPaths,
                    currentPath,
                    SelectedProjectMediaRoot()),
                ValueKind.MediaDirectoryPath => BrowseMediaDirectory(
                    _storageProvider,
                    _projectPaths,
                    currentPath,
                    SelectedProjectMediaRoot()),
                ValueKind.VideoFilePath => BrowseProjectVideoFile(
                    _storageProvider,
                    _projectPaths,
                    currentPath),
                ValueKind.JsonFilePath => BrowseJsonFile(
                    _storageProvider,
                    currentPath),
                _ => BrowseDirectory(currentPath),
            });
        }
        catch (Exception exception) when (valueKind == ValueKind.VideoFilePath)
        {
            await _showInfo(
                "Reference video not associated",
                $"The selected video could not be associated with this Shot. {exception.Message}");
            return null;
        }
        catch (Exception exception) when (valueKind == ValueKind.MediaDirectoryPath)
        {
            await _showInfo(
                "Media folder not associated",
                $"The selected folder could not be associated with this Project. {exception.Message}");
            return null;
        }
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
            var fullMediaRoot = _projectPaths.ResolveProjectPath(mediaRoot);
            if (Directory.Exists(fullMediaRoot))
            {
                options.SuggestedStartLocation = await _storageProvider.TryGetFolderFromPathAsync(fullMediaRoot);
            }
        }

        var files = await _storageProvider.OpenFilePickerAsync(options);
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public async Task<string?> BrowseExternalMediaSourceDirectory(
        string currentDirectory)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = "Select replacement source directory",
            AllowMultiple = false,
        };
        if (!string.IsNullOrWhiteSpace(currentDirectory)
            && Directory.Exists(currentDirectory))
        {
            options.SuggestedStartLocation =
                await _storageProvider.TryGetFolderFromPathAsync(
                    Path.GetFullPath(currentDirectory));
        }

        var folders = await _storageProvider.OpenFolderPickerAsync(options);
        return folders.Count > 0
            ? Path.GetFullPath(folders[0].Path.LocalPath)
            : null;
    }

    public string ExternalMediaStoragePath(
        string absolutePath,
        ValueKind valueKind,
        string projectId)
    {
        var fullPath = Path.GetFullPath(absolutePath);
        if (valueKind == ValueKind.VideoFilePath)
        {
            return ReferenceVideoStoragePath(_projectPaths, fullPath);
        }

        var stored = _projectPaths.RelativePathIfInsideMediaRoot(
            fullPath,
            _contextData.ProjectMediaRoot(projectId)) ?? fullPath;
        if (valueKind == ValueKind.MediaDirectoryPath
            && Path.IsPathFullyQualified(stored))
        {
            throw new InvalidOperationException(
                "Media directories must remain inside the Project media root.");
        }
        return _projectPaths.NormalizeRelativePath(stored);
    }

    private static async Task<string?> BrowseJsonFile(
        IStorageProvider storageProvider,
        string currentPath)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Select Shot Manager production.json",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("JSON")
                {
                    Patterns = ["*.json"],
                    AppleUniformTypeIdentifiers = ["public.json"],
                    MimeTypes = ["application/json"],
                },
            ],
        };
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var parent = Path.GetDirectoryName(
                Path.GetFullPath(currentPath));
            if (!string.IsNullOrWhiteSpace(parent)
                && Directory.Exists(parent))
            {
                options.SuggestedStartLocation =
                    await storageProvider.TryGetFolderFromPathAsync(parent);
            }
        }
        var files = await storageProvider.OpenFilePickerAsync(options);
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }

    public string? ResolveImagePath(string path)
    {
        return _projectPaths.ResolveLocalPath(
            path,
            SelectedProjectMediaRoot());
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
            var fullPath = _projectPaths.ResolveProjectPath(currentPath);
            if (Directory.Exists(fullPath))
            {
                options.SuggestedStartLocation = await _storageProvider.TryGetFolderFromPathAsync(fullPath);
            }
        }

        var folders = await _storageProvider.OpenFolderPickerAsync(options);
        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }

    private static async Task<string?> BrowseMediaDirectory(
        IStorageProvider storageProvider,
        IProjectPathResolver projectPaths,
        string currentPath,
        string? mediaRoot)
    {
        var options = new FolderPickerOpenOptions
        {
            Title = "Select media folder",
            AllowMultiple = false,
        };
        var fullMediaRoot = string.IsNullOrWhiteSpace(mediaRoot)
            ? null
            : projectPaths.ResolveProjectPath(mediaRoot);
        var currentFullPath = string.IsNullOrWhiteSpace(currentPath)
            ? fullMediaRoot
            : Path.IsPathFullyQualified(currentPath)
                ? currentPath
                : fullMediaRoot is not null
                    ? Path.GetFullPath(Path.Combine(fullMediaRoot, currentPath))
                    : projectPaths.ResolveProjectPath(currentPath);
        if (!string.IsNullOrWhiteSpace(currentFullPath) && Directory.Exists(currentFullPath))
        {
            options.SuggestedStartLocation =
                await storageProvider.TryGetFolderFromPathAsync(currentFullPath);
        }

        var folders = await storageProvider.OpenFolderPickerAsync(options);
        if (folders.Count == 0) return null;
        var selectedPath = folders[0].Path.LocalPath;
        var relative = projectPaths.RelativePathIfInsideMediaRoot(selectedPath, mediaRoot);
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathFullyQualified(relative))
        {
            throw new InvalidOperationException(
                "The selected media folder must be inside the Project media root.");
        }
        return relative;
    }

    private string? SelectedProjectMediaRoot()
    {
        var selectedNode = _selectedNode();
        if (selectedNode is null) return null;

        var project = ProjectAncestor(selectedNode);
        return _contextData.ProjectMediaRoot(project.Id);
    }

    public static async Task<string?> BrowseImageFile(
        IStorageProvider storageProvider,
        IProjectPathResolver projectPaths,
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
                    ? Path.GetFullPath(Path.Combine(projectPaths.ResolveProjectPath(mediaRoot), currentPath))
                    : projectPaths.ResolveProjectPath(currentPath);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(parent);
            }
        }

        var files = await storageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return null;

        var selectedPath = files[0].Path.LocalPath;
        return projectPaths.RelativePathIfInsideMediaRoot(
            selectedPath,
            mediaRoot);
    }

    public static async Task<string?> BrowseMediaFile(
        IStorageProvider storageProvider,
        IProjectPathResolver projectPaths,
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
                    ? Path.GetFullPath(Path.Combine(projectPaths.ResolveProjectPath(mediaRoot), currentPath))
                    : projectPaths.ResolveProjectPath(currentPath);
            var parent = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(parent) && Directory.Exists(parent))
            {
                options.SuggestedStartLocation = await storageProvider.TryGetFolderFromPathAsync(parent);
            }
        }

        var files = await storageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return null;

        var selectedPath = files[0].Path.LocalPath;
        return projectPaths.RelativePathIfInsideMediaRoot(
            selectedPath,
            mediaRoot) ?? selectedPath;
    }

    private static async Task<string?> BrowseProjectVideoFile(
        IStorageProvider storageProvider,
        IProjectPathResolver projectPaths,
        string currentPath)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Select Shot reference video",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Video")
                {
                    Patterns = ["*.mp4", "*.mov", "*.m4v", "*.webm"],
                    AppleUniformTypeIdentifiers = ["public.movie", "public.video"],
                    MimeTypes = ["video/mp4", "video/quicktime", "video/webm"],
                },
            ],
        };
        var currentFullPath = string.IsNullOrWhiteSpace(currentPath)
            ? projectPaths.ProjectRoot
            : projectPaths.ResolveProjectPath(currentPath);
        var start = Directory.Exists(currentFullPath)
            ? currentFullPath
            : Path.GetDirectoryName(currentFullPath);
        if (!string.IsNullOrWhiteSpace(start) && Directory.Exists(start))
        {
            options.SuggestedStartLocation =
                await storageProvider.TryGetFolderFromPathAsync(start);
        }

        var files = await storageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return null;
        var selectedPath = files[0].Path.LocalPath;
        if (!File.Exists(selectedPath))
        {
            throw new InvalidOperationException(
                "The selected file no longer exists or is not available.");
        }
        if (Path.GetExtension(selectedPath).ToLowerInvariant()
            is not (".mp4" or ".mov" or ".m4v" or ".webm"))
        {
            throw new InvalidOperationException(
                "Choose an MP4, MOV, M4V or WebM video file.");
        }
        return ReferenceVideoStoragePath(
            projectPaths,
            selectedPath);
    }

    internal static string ReferenceVideoStoragePath(
        IProjectPathResolver projectPaths,
        string selectedPath)
    {
        var relative = Path.GetRelativePath(
            projectPaths.ProjectRoot,
            selectedPath);
        if (Path.IsPathFullyQualified(relative)
            || relative.Replace('\\', '/').Split('/').Any(
                (segment) => segment == ".."))
        {
            return Path.GetFullPath(selectedPath);
        }
        return projectPaths.NormalizeRelativePath(relative);
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
