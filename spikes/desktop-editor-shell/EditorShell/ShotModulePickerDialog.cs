using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ShotModulePickerDialog
{
    private readonly Window _owner;
    private readonly SpikeDatabase _database;

    public ShotModulePickerDialog(Window owner, SpikeDatabase database)
    {
        _owner = owner;
        _database = database;
    }

    public Task<SpikeDatabase.ShotModuleInstanceDraft?> Show(string shotId)
    {
        var modules = _database.GetAvailableShotModules(shotId);
        var dialog = new SukiWindow
        {
            Title = "Add Screen to Shot",
            Width = 540,
            Height = 360,
            MinWidth = 460,
            MinHeight = 340,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

        var moduleOptions = modules
            .Select((module) => new FieldOption(module.Id, modules.Count((candidate) => candidate.Name == module.Name) > 1
                ? $"{module.Name} · {module.AppName}"
                : module.Name))
            .ToList();
        var moduleCombo = new EditorInstantComboBox
        {
            ItemsSource = moduleOptions,
            SelectedItem = moduleOptions.FirstOrDefault(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var variantCombo = new EditorInstantComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        var nameBox = EditorTextBoxBehavior.Configure(new TextBox
        {
            MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
        });
        var addButton = new Button
        {
            Content = "Add Screen",
            MinWidth = 92,
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            MinWidth = 92,
        };

        var nameEdited = false;
        var automaticName = "";
        SpikeDatabase.ShotModuleChoice? SelectedModule() => modules.FirstOrDefault((module) => module.Id == moduleCombo.SelectedItem?.Value);
        void RefreshAddButton() => addButton.IsEnabled = SelectedModule() is not null
            && variantCombo.SelectedItem is not null
            && !string.IsNullOrWhiteSpace(nameBox.Text);
        void ApplyDefaultName()
        {
            if (nameEdited) return;
            var module = SelectedModule();
            var variant = variantCombo.SelectedItem;
            automaticName = module is null || variant is null ? "" : $"{module.Name} · {variant.Label}";
            nameBox.Text = automaticName;
            RefreshAddButton();
        }
        void RefreshVariants()
        {
            var module = SelectedModule();
            var options = module is null ? [] : _database.GetModuleVariantOptions(module.Id).ToList();
            variantCombo.ItemsSource = options;
            variantCombo.SelectedItem = options.FirstOrDefault();
            ApplyDefaultName();
            RefreshAddButton();
        }
        void Commit()
        {
            var module = SelectedModule();
            var variant = variantCombo.SelectedItem;
            var name = nameBox.Text?.Trim();
            if (module is null || variant is null || string.IsNullOrWhiteSpace(name)) return;
            dialog.Close(new SpikeDatabase.ShotModuleInstanceDraft(module, variant.Value, variant.Label, name));
        }

        moduleCombo.SelectionChanged += (_, _) => RefreshVariants();
        variantCombo.SelectionChanged += (_, _) => ApplyDefaultName();
        nameBox.TextChanged += (_, _) =>
        {
            var currentName = nameBox.Text ?? "";
            nameEdited = !string.IsNullOrWhiteSpace(currentName)
                && !currentName.Equals(automaticName, StringComparison.Ordinal);
            if (!nameEdited && string.IsNullOrWhiteSpace(currentName)) ApplyDefaultName();
            RefreshAddButton();
        };
        nameBox.KeyDown += (_, eventArgs) =>
        {
            if (eventArgs.Key != Key.Enter || addButton.IsEnabled != true) return;
            eventArgs.Handled = true;
            Commit();
        };
        cancelButton.Click += (_, _) => dialog.Close(null);
        addButton.Click += (_, _) => Commit();

        var fields = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("110,*"),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnSpacing = 12,
            RowSpacing = 10,
        };
        AddField("Module", moduleCombo, 0);
        AddField("Variant", variantCombo, 1);
        AddField("Name", nameBox, 2);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancelButton, addButton },
        };
        var content = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = "Choose the Module and its Variant, then name this Screen in the Shot.",
                    TextWrapping = TextWrapping.Wrap,
                },
                fields,
            },
        };
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 18,
            Children = { content, actions },
        };
        Grid.SetRow(actions, 1);
        dialog.Content = new Border
        {
            Padding = EditorUiDensity.CardThickness(18),
            Child = root,
        };
        dialog.Opened += (_, _) =>
        {
            RefreshVariants();
            RefreshAddButton();
            if (modules.Count == 0) moduleCombo.IsEnabled = false;
        };
        return dialog.ShowDialog<SpikeDatabase.ShotModuleInstanceDraft?>(_owner);

        void AddField(string label, Control control, int row)
        {
            var text = new TextBlock
            {
                Text = label,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetRow(text, row);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            fields.Children.Add(text);
            fields.Children.Add(control);
        }
    }
}
