using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorTextBoxBehavior
{
    public static TextBox Configure(TextBox textBox)
    {
        textBox.ClearSelectionOnLostFocus = false;
        textBox.ContextFlyout = null;
        textBox.ContextMenu = null;
        textBox.Padding = new Thickness(6, textBox.Padding.Top, 6, textBox.Padding.Bottom);
        textBox.AddHandler(
            InputElement.TextInputEvent,
            (_, args) => ApplyTextInputFallback(textBox, args),
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        return textBox;
    }

    public static void AttachDeferredCommit(TextBox textBox, Action commit, bool commitOnEnter = true)
    {
        textBox.LostFocus += (_, _) => commit();
        textBox.KeyDown += (_, args) =>
        {
            if (args.Key != Key.Enter || !commitOnEnter) return;

            Dispatcher.UIThread.Post(commit);
        };
    }

    private static void ApplyTextInputFallback(TextBox textBox, TextInputEventArgs args)
    {
        var input = args.Text;
        if (string.IsNullOrEmpty(input) || textBox.IsReadOnly)
        {
            return;
        }

        var before = textBox.Text ?? "";
        var selectionStart = Math.Clamp(textBox.SelectionStart, 0, before.Length);
        var selectionEnd = Math.Clamp(textBox.SelectionEnd, 0, before.Length);
        var insertAt = Math.Min(selectionStart, selectionEnd);
        var replaceLength = Math.Abs(selectionEnd - selectionStart);

        Dispatcher.UIThread.Post(() =>
        {
            if ((textBox.Text ?? "") != before)
            {
                return;
            }

            var next = before.Remove(insertAt, replaceLength).Insert(insertAt, input);
            textBox.Text = next;
            textBox.CaretIndex = insertAt + input.Length;
        });
    }
}
