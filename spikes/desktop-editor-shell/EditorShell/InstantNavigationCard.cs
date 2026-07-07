using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SukiUI.Controls;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class InstantNavigationCard : Border
{
    public InstantNavigationCard(Control header, Control content, bool isExpanded)
    {
        Margin = new Thickness(0, 0, 0, 4);
        CornerRadius = new CornerRadius(10);
        BoxShadow = BoxShadows.Parse("0 4 10 0 #18000000");

        var contentHost = new Border
        {
            Padding = new Thickness(7, 0, 7, 7),
            IsVisible = isExpanded,
            Child = content,
        };

        Child = new GlassCard
        {
            Content = new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    header,
                    contentHost,
                },
            },
        };

        if (isExpanded)
        {
            AttachedToVisualTree += (_, _) => DeferredBringIntoView.Request(contentHost);
        }
    }
}
