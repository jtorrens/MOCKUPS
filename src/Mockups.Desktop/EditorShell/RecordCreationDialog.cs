using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class RecordCreationDialog
{
    internal const double DialogWidth = 820;
    internal const double DialogMinimumWidth = 720;

    private readonly Window _owner;

    public RecordCreationDialog(Window owner) => _owner = owner;

    public async Task<RecordCreationDraft?> Show(RecordCreationDefinition definition)
    {
        var values = definition.Fields.ToDictionary(
            (field) => field.Definition.Id, (field) => field.Value, StringComparer.Ordinal);
        var dialog = new SukiWindow
        {
            Title = definition.Title,
            Width = DialogWidth,
            Height = Math.Min(720, 260 + definition.Fields.Count * 72),
            MinWidth = DialogMinimumWidth,
            MinHeight = 300,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            IsMenuVisible = false,
            BackgroundAnimationEnabled = false,
            BackgroundTransitionsEnabled = false,
            BackgroundTransitionTime = 0.05,
        };
        EditorSukiWindowTheme.ApplyDialogChrome(dialog, _owner);

        var validation = new TextBlock { Opacity = 0.72, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var accept = new Button { Content = definition.ActionLabel, MinWidth = 92 };
        void Refresh()
        {
            var error = definition.ValidationError(values);
            accept.IsEnabled = error is null;
            validation.Text = error ?? "All required values are complete.";
        }
        var fields = new StackPanel { Spacing = 12 };
        foreach (var fieldValue in definition.Fields)
        {
            var field = new DictionaryFieldControl(
                fieldValue,
                new DictionaryFieldServices(AllowIncompleteDraft: true));
            void Changed(string value)
            {
                values[fieldValue.Definition.Id] = value;
                Refresh();
            }
            field.ValueChanged += (_, value) => Changed(value);
            field.ValueCommitted += (_, value) => Changed(value);
            fields.Children.Add(field);
        }
        var cancel = new Button { Content = "Cancel", MinWidth = 92 };
        cancel.Click += (_, _) => dialog.Close(null);
        accept.Click += (_, _) =>
        {
            if (definition.ValidationError(values) is not null) return;
            dialog.Close(new RecordCreationDraft(
                definition.Id,
                new Dictionary<string, string>(values, StringComparer.Ordinal)));
        };
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10,
            Children = { cancel, accept },
        };
        var body = new StackPanel
        {
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = definition.Description, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                fields,
                validation,
            },
        };
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            RowSpacing = 18,
            Children =
            {
                new ScrollViewer
                {
                    HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                    Content = body,
                },
                actions,
            },
        };
        Grid.SetRow(actions, 1);
        dialog.Content = new Border { Padding = EditorUiDensity.CardThickness(18), Child = root };
        Refresh();
        return await dialog.ShowDialog<RecordCreationDraft?>(_owner);
    }
}
