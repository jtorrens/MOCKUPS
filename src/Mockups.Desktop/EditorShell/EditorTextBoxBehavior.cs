using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class EditorTextBoxBehavior
{
    private static readonly ConditionalWeakTable<TextBox, TextBoxInteractionState> InteractionStates = new();

    public static TextBox Configure(TextBox textBox)
    {
        EnsureInteractionState(textBox);
        textBox.ClearSelectionOnLostFocus = false;
        textBox.ContextFlyout = null;
        textBox.ContextMenu = null;
        textBox.Padding = new Thickness(6, textBox.Padding.Top, 6, textBox.Padding.Bottom);
        return textBox;
    }

    public static void EnableSelectAllOnDoubleClick(TextBox textBox)
    {
        EnsureInteractionState(textBox).SelectAllOnDoubleClick = true;
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

    private static TextBoxInteractionState EnsureInteractionState(TextBox textBox)
    {
        return InteractionStates.GetValue(textBox, static configuredTextBox =>
        {
            var state = new TextBoxInteractionState(configuredTextBox);
            configuredTextBox.AddHandler(
                InputElement.TextInputEvent,
                (_, args) => ApplyTextInputFallback(configuredTextBox, args),
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
            state.Attach();
            return state;
        });
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

    private sealed class TextBoxInteractionState(TextBox textBox)
    {
        private readonly TextBox _textBox = textBox;
        private IPointer? _penPointer;
        private int _selectionAnchor;

        public bool SelectAllOnDoubleClick { get; set; }

        public void Attach()
        {
            _textBox.AddHandler(
                InputElement.PointerPressedEvent,
                (_, args) => OnPointerPressed(args),
                RoutingStrategies.Bubble,
                handledEventsToo: true);
            _textBox.AddHandler(
                InputElement.PointerMovedEvent,
                (_, args) => OnPointerMoved(args),
                RoutingStrategies.Bubble,
                handledEventsToo: true);
            _textBox.AddHandler(
                InputElement.PointerReleasedEvent,
                (_, args) => OnPointerReleased(args),
                RoutingStrategies.Bubble,
                handledEventsToo: true);
            _textBox.AddHandler(
                InputElement.PointerCaptureLostEvent,
                (_, _) => ResetPenSelection(),
                RoutingStrategies.Bubble,
                handledEventsToo: true);
        }

        private void OnPointerPressed(PointerPressedEventArgs args)
        {
            if (SelectAllOnDoubleClick
                && args.ClickCount == 2
                && IsPrimarySelectionPointer(args))
            {
                _textBox.SelectAll();
                ResetPenSelection();
                args.Handled = true;
                return;
            }

            if (args.Pointer.Type != PointerType.Pen
                || args.ClickCount != 1
                || !IsPrimarySelectionPointer(args)
                || !TryMoveCaret(args, out var caretIndex, out var presenter))
            {
                return;
            }

            _selectionAnchor = args.KeyModifiers.HasFlag(KeyModifiers.Shift)
                ? Math.Clamp(_textBox.SelectionStart, 0, (_textBox.Text ?? "").Length)
                : caretIndex;
            _penPointer = args.Pointer;
            SetSelection(caretIndex);
            args.Pointer.Capture(presenter);
            args.PreventGestureRecognition();
            args.Handled = true;
        }

        private void OnPointerMoved(PointerEventArgs args)
        {
            if (!ReferenceEquals(args.Pointer, _penPointer)
                || !TryMoveCaret(args, out var caretIndex, out _))
            {
                return;
            }

            SetSelection(caretIndex);
            args.PreventGestureRecognition();
            args.Handled = true;
        }

        private void OnPointerReleased(PointerReleasedEventArgs args)
        {
            if (!ReferenceEquals(args.Pointer, _penPointer))
            {
                return;
            }

            if (TryMoveCaret(args, out var caretIndex, out _))
            {
                SetSelection(caretIndex);
            }

            ResetPenSelection();
            args.Handled = true;
        }

        private bool IsPrimarySelectionPointer(PointerPressedEventArgs args)
        {
            if (args.Pointer.Type is not (PointerType.Mouse or PointerType.Pen))
            {
                return false;
            }

            var properties = args.GetCurrentPoint(_textBox).Properties;
            return !properties.IsRightButtonPressed
                && !properties.IsMiddleButtonPressed
                && !properties.IsBarrelButtonPressed
                && !properties.IsEraser
                && (args.Pointer.Type == PointerType.Pen || properties.IsLeftButtonPressed);
        }

        private bool TryMoveCaret(
            PointerEventArgs args,
            out int caretIndex,
            out TextPresenter presenter)
        {
            presenter = _textBox
                .GetVisualDescendants()
                .OfType<TextPresenter>()
                .FirstOrDefault()!;
            if (presenter is null)
            {
                caretIndex = 0;
                return false;
            }

            var point = args.GetPosition(presenter);
            point = new Point(
                Math.Clamp(point.X, 0, Math.Max(presenter.Bounds.Width - 1, 0)),
                Math.Clamp(point.Y, 0, Math.Max(presenter.Bounds.Height - 1, 0)));
            presenter.MoveCaretToPoint(point);
            caretIndex = Math.Clamp(presenter.CaretIndex, 0, (_textBox.Text ?? "").Length);
            return true;
        }

        private void SetSelection(int caretIndex)
        {
            _textBox.SetCurrentValue(TextBox.CaretIndexProperty, caretIndex);
            _textBox.SetCurrentValue(TextBox.SelectionStartProperty, _selectionAnchor);
            _textBox.SetCurrentValue(TextBox.SelectionEndProperty, caretIndex);
        }

        private void ResetPenSelection()
        {
            _penPointer = null;
        }
    }
}
