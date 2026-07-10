using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorAddChildWorkflow
{
    private readonly Window _owner;
    private readonly SpikeDatabase _database;
    private readonly Func<string, string, Task> _showInfo;

    public EditorAddChildWorkflow(
        Window owner,
        SpikeDatabase database,
        Func<string, string, Task> showInfo)
    {
        _owner = owner;
        _database = database;
        _showInfo = showInfo;
    }

    public async Task<ProjectTreeNode?> TryAdd(ProjectTreeNode parent)
    {
        if (parent.Kind == ProjectTreeNodeKind.ProductionFontsRoot)
        {
            return await ImportProductionFont(parent);
        }

        if (parent.Kind == ProjectTreeNodeKind.IconThemesRoot)
        {
            await RefreshIconThemes(parent);
            return parent;
        }

        if (parent.Kind == ProjectTreeNodeKind.ThemesRoot)
        {
            var preset = await ChooseThemePreset();
            return preset is null ? null : _database.AddTheme(parent, preset);
        }

        if (parent.Kind == ProjectTreeNodeKind.DevicesRoot)
        {
            return await ImportDevice(parent);
        }

        return _database.AddChild(parent);
    }

    private async Task<ProjectTreeNode?> ImportDevice(ProjectTreeNode devicesRoot)
    {
        try
        {
            var dialog = new DeviceImportDialog(_owner, new PhoneSpecsDeviceCatalogProvider());
            var result = await dialog.ShowAsync();
            if (result is null) return null;
            if (result.CreateBlank) return _database.AddChild(devicesRoot);
            return result.Draft is null ? null : _database.AddImportedDevice(devicesRoot, result.Draft);
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
            var result = _database.RefreshIconThemeSets(parent);
            await _showInfo("Refresh complete", $"Refreshed {result.CommonTokenCount} common token(s) across {result.ThemeCount} icon set(s). Omitted {result.OmittedTokenCount} token(s) not present in every set.");
        }
        catch (Exception exception)
        {
            await _showInfo("Refresh failed", exception.Message);
        }
    }

    private async Task<string?> ChooseThemePreset()
    {
        var dialog = new SukiWindow
        {
            Title = "Create theme",
            Width = 420,
            Height = 230,
            MinWidth = 420,
            MinHeight = 230,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

        var cancelButton = new Button { Content = "Cancel", MinWidth = 92 };
        cancelButton.Click += (_, _) => dialog.Close(null);
        var iosButton = new Button { Content = "iOS preset", MinWidth = 110 };
        iosButton.Click += (_, _) => dialog.Close("ios");
        var androidButton = new Button { Content = "Android preset", MinWidth = 130 };
        androidButton.Click += (_, _) => dialog.Close("android");

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children =
            {
                cancelButton,
                iosButton,
                androidButton,
            },
        };

        dialog.Content = new Border
        {
            Padding = new Thickness(22),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                RowSpacing = 18,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Choose a starting preset for the new theme.",
                                TextWrapping = TextWrapping.Wrap,
                            },
                            new TextBlock
                            {
                                Text = "You can edit the linked icon, status bar, navigation bar and tokens afterwards.",
                                Opacity = 0.72,
                                TextWrapping = TextWrapping.Wrap,
                            },
                        },
                    },
                    actions,
                },
            },
        };
        Grid.SetRow(actions, 1);

        return await dialog.ShowDialog<string?>(_owner);
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
        var mediaRoot = _database.GetProjectSettings(project.Id).MediaRoot;
        var fullMediaRoot = ResolveProjectMediaRoot(mediaRoot);
        if (!string.IsNullOrWhiteSpace(fullMediaRoot) && Directory.Exists(fullMediaRoot))
        {
            options.SuggestedStartLocation = await _owner.StorageProvider.TryGetFolderFromPathAsync(fullMediaRoot);
        }

        var files = await _owner.StorageProvider.OpenFilePickerAsync(options);
        if (files.Count == 0) return null;

        try
        {
            return _database.ImportProductionFont(fontsRoot, files.Select((file) => file.Path.LocalPath).ToList());
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

    private static string ResolveProjectMediaRoot(string mediaRoot)
    {
        return ProjectPathService.ResolveProjectPath(mediaRoot);
    }
}
