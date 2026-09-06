using Avalonia;
using Avalonia.Controls;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record DeferredEditorCardContent(
    string Subtitle,
    Control Content);

internal static class DeferredEditorCard
{
    public static InstantEditorCard Create<T>(
        string title,
        string loadingSubtitle,
        Func<Control> createIcon,
        string sessionStateId,
        Func<CancellationToken, Task<T>> load,
        Func<T, DeferredEditorCardContent> present,
        Control? headerTrailing = null,
        bool isExpanded = false)
    {
        var header = new ContentControl
        {
            Content = EditorCardHeader.Create(
                title,
                loadingSubtitle,
                createIcon()),
        };
        var content = new ContentControl
        {
            Content = Status("Loading…"),
        };
        var card = new InstantEditorCard(
            header,
            content,
            isExpanded: false,
            headerTrailing)
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            SessionStateId = sessionStateId,
        };
        var lifetime = new CancellationTokenSource();
        var cancellationToken = lifetime.Token;
        var loading = false;
        var loaded = false;
        var disposed = false;

        card.Expanded += async (_, _) =>
        {
            if (loading || loaded || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            loading = true;
            content.Content = Status("Loading…");
            try
            {
                var snapshot = await load(cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var presented = present(snapshot);
                header.Content = EditorCardHeader.Create(
                    title,
                    presented.Subtitle,
                    createIcon());
                content.Content = presented.Content;
                loaded = true;
            }
            catch (OperationCanceledException) when (
                cancellationToken.IsCancellationRequested)
            {
                // Removing the card cancels its queued persistence read.
            }
            catch (Exception exception)
            {
                header.Content = EditorCardHeader.Create(
                    title,
                    "Unable to load",
                    createIcon());
                content.Content = Status(exception.Message);
            }
            finally
            {
                loading = false;
            }
        };
        card.DetachedFromVisualTree += (_, _) =>
        {
            if (disposed) return;
            disposed = true;
            lifetime.Cancel();
            lifetime.Dispose();
        };
        if (isExpanded)
        {
            card.IsExpanded = true;
        }
        return card;
    }

    private static Border Status(string text) => new()
    {
        Padding = EditorUiDensity.CardThickness(10),
        Child = new TextBlock
        {
            Text = text,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Opacity = 0.72,
        },
    };
}
