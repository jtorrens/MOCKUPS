using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotModulePickerDialog
{
    private readonly Window _owner;

    public ShotModulePickerDialog(Window owner)
    {
        _owner = owner;
    }

    public Task<SpikeDatabase.ShotModuleChoice?> Show(IReadOnlyList<SpikeDatabase.ShotModuleChoice> modules)
    {
        var dialog = new SukiWindow
        {
            Title = "Add module to Shot",
            Width = 520,
            Height = 460,
            MinWidth = 440,
            MinHeight = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);
        var list = new StackPanel { Spacing = EditorUiDensity.Card(6) };
        foreach (var module in modules)
        {
            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new StackPanel
                {
                    Spacing = 2,
                    Children =
                    {
                        new TextBlock { Text = module.Name, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                        new TextBlock { Text = module.AppName, Opacity = 0.68, FontSize = 11 },
                    },
                },
            };
            button.Click += (_, _) => dialog.Close(module);
            list.Children.Add(button);
        }
        if (modules.Count == 0)
            list.Children.Add(new TextBlock { Text = "This project has no modules available.", Opacity = 0.72 });

        var cancel = new Button { Content = "Cancel", MinWidth = 92 };
        cancel.Click += (_, _) => dialog.Close(null);
        dialog.Content = new Border
        {
            Padding = EditorUiDensity.CardThickness(18),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                RowSpacing = EditorUiDensity.Card(12),
                Children =
                {
                    new ScrollViewer { Content = list },
                    cancel,
                },
            },
        };
        Grid.SetRow(cancel, 1);
        cancel.HorizontalAlignment = HorizontalAlignment.Right;
        return dialog.ShowDialog<SpikeDatabase.ShotModuleChoice?>(_owner);
    }
}
