using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DraggablePreviewSurface : Grid
{
    private readonly TranslateTransform _translate = new();
    private Point _lastPosition;
    private bool _isDragging;

    public DraggablePreviewSurface(Control child, double scale)
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.SizeAll);

        var transforms = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(scale, scale),
                _translate,
            },
        };

        child.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        child.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
        child.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        child.RenderTransform = transforms;
        Children.Add(child);

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += (_, _) => _isDragging = false;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (!args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isDragging = true;
        _lastPosition = args.GetPosition(this);
        args.Pointer.Capture(this);
        args.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        if (!_isDragging)
        {
            return;
        }

        var position = args.GetPosition(this);
        var delta = position - _lastPosition;
        _translate.X += delta.X;
        _translate.Y += delta.Y;
        _lastPosition = position;
        args.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs args)
    {
        _isDragging = false;
        args.Pointer.Capture(null);
        args.Handled = true;
    }
}
