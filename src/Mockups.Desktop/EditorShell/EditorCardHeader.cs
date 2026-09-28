using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorCardHeader
{
    public static Control Create(
        EditorInternalNavigationPresentation presentation)
    {
        var iconHost = new ContentControl();
        var label = new TextBlock
        {
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var subtitle = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.72,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var textPanel = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                label,
                subtitle,
            },
        };
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
            Children =
            {
                iconHost,
            },
        };
        Grid.SetColumn(textPanel, 1);
        header.Children.Add(textPanel);

        void Apply()
        {
            label.Text = presentation.Label;
            subtitle.Text = presentation.Subtitle;
            subtitle.IsVisible = !string.IsNullOrWhiteSpace(
                presentation.Subtitle);
            iconHost.Content = EditorIcons.CreateSemantic(
                presentation.Label,
                presentation.Icon,
                15);
        }

        presentation.Changed += Apply;
        Apply();
        return header;
    }

    public static Control Create(string label, string subtitle, Control icon)
    {
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"),
            ColumnSpacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
            ClipToBounds = true,
        };
        header.Children.Add(icon);

        var textPanel = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
        };
        textPanel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            textPanel.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 12,
                Opacity = 0.72,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }

        Grid.SetColumn(textPanel, 1);
        header.Children.Add(textPanel);
        return header;
    }

    public static string Subtitle(EditorLayoutCard layoutCard)
    {
        if (!string.IsNullOrWhiteSpace(layoutCard.Subtitle))
        {
            return layoutCard.Subtitle;
        }

        return layoutCard.VisibleGroups
            .Select((group) => group.Label)
            .FirstOrDefault((label) => !string.IsNullOrWhiteSpace(label)) ?? "";
    }

    public static void SetOverrideState(Control icon, IEnumerable<DictionaryFieldControl> controls)
    {
        EditorIcons.ApplyBrush(icon, controls.Any((control) => control.HasOverrides) ? EditorOverrideVisuals.Brush : null);
    }
}
