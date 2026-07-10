using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SukiUI.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class EditorCardHostController
{
    private readonly Panel _host;
    private readonly List<InstantEditorCard> _cards = [];
    private readonly List<Border> _wrappers = [];
    private readonly Func<double>? _availableWidth;

    public EditorCardHostController(Panel host, Func<double>? availableWidth = null, Control? widthObserver = null)
    {
        _host = host;
        _availableWidth = availableWidth;
        if (widthObserver is not null)
        {
            widthObserver.SizeChanged += (_, _) => UpdateWrapperWidths();
        }
    }

    public IReadOnlyList<InstantEditorCard> Cards => _cards;

    public void Clear()
    {
        _cards.Clear();
        _wrappers.Clear();
        _host.Children.Clear();
    }

    public void Add(InstantEditorCard card)
    {
        card.Expanded += (_, _) =>
        {
            foreach (var other in _cards.Where((item) => item != card))
            {
                other.IsExpanded = false;
            }
        };

        _cards.Add(card);
        var wrapper = new Border
        {
            Margin = EditorUiDensity.CardThickness(0, 0, 0, 12),
            CornerRadius = new CornerRadius(14),
            BoxShadow = BoxShadows.Parse("0 6 14 0 #22000000"),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Child = new GlassCard
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                Content = card,
            },
        };
        _wrappers.Add(wrapper);
        _host.Children.Add(wrapper);
        UpdateWrapperWidths();
    }

    private void UpdateWrapperWidths()
    {
        var width = _availableWidth?.Invoke() ?? 0;
        if (width <= 0)
        {
            return;
        }

        foreach (var wrapper in _wrappers)
        {
            wrapper.Width = width;
        }
    }
}
