using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorAccordionCard : Border
{
    private readonly TextBlock _chevron;
    private readonly ContentControl _bodyHost;
    private bool _isOpen;
    private bool _isChanged;

    public EditorAccordionCard(string title, string icon, Control body, bool isOpen = false)
    {
        Classes.Add("editor-accordion-card");

        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
        };

        var headerButton = new Button
        {
            Padding = new Avalonia.Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
        };
        headerButton.Click += OnHeaderClick;

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            ColumnSpacing = 12,
            MinHeight = 56,
            Margin = new Avalonia.Thickness(14, 10),
        };

        var iconBlock = new TextBlock
        {
            Text = icon,
            Width = 24,
            Height = 24,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            FontWeight = FontWeight.Bold,
        };
        iconBlock.Classes.Add("accordion-icon");
        Grid.SetColumn(iconBlock, 0);

        var titleBlock = new TextBlock
        {
            Text = title,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 15,
            FontWeight = FontWeight.Bold,
        };
        titleBlock.Classes.Add("accordion-title");
        Grid.SetColumn(titleBlock, 1);

        _chevron = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontSize = 18,
            FontWeight = FontWeight.Bold,
        };
        _chevron.Classes.Add("accordion-chevron");
        Grid.SetColumn(_chevron, 2);

        header.Children.Add(iconBlock);
        header.Children.Add(titleBlock);
        header.Children.Add(_chevron);
        headerButton.Content = header;
        Grid.SetRow(headerButton, 0);

        _bodyHost = new ContentControl
        {
            Content = body,
            Padding = new Avalonia.Thickness(14),
        };
        _bodyHost.Classes.Add("accordion-body");
        Grid.SetRow(_bodyHost, 1);

        root.Children.Add(headerButton);
        root.Children.Add(_bodyHost);
        Child = root;

        SetOpen(isOpen, notify: false);
    }

    public event EventHandler? Opened;

    public bool IsOpen => _isOpen;

    public bool IsChanged
    {
        get => _isChanged;
        set
        {
            _isChanged = value;
            PseudoClasses.Set(":changed", value);
        }
    }

    public void SetOpen(bool value, bool notify = true)
    {
        _isOpen = value;
        _bodyHost.IsVisible = value;
        _chevron.Text = value ? "⌃" : "›";
        PseudoClasses.Set(":open", value);
        if (value && notify)
        {
            Opened?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnHeaderClick(object? sender, RoutedEventArgs e)
    {
        SetOpen(!IsOpen);
    }
}
