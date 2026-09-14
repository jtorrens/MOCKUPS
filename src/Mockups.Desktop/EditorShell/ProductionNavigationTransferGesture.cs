using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Primitives.PopupPositioning;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Common;
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal static class ProductionNavigationTransferGesture
{
    private static readonly ConditionalWeakTable<
        Border,
        TransferTarget> Targets = new();

    internal static void Attach(
        Border row,
        ProjectTreeNode node,
        Func<
            ProjectTreeNode,
            ProjectTreeNode,
            ProductionHierarchyTransferMode,
            Task> transfer,
        Action<Exception> reportFailure)
    {
        if (node.Kind is ProjectTreeNodeKind.Episode
            or ProjectTreeNodeKind.Shot)
        {
            Targets.Add(
                row,
                new TransferTarget(
                    row,
                    node,
                    row.BorderBrush,
                    row.BorderThickness));
        }

        if (node.Kind is not ProjectTreeNodeKind.Shot
            and not ProjectTreeNodeKind.ModuleInstance)
        {
            return;
        }

        Point pressOrigin = default;
        IPointer? pointer = null;
        ProductionHierarchyTransferMode? mode = null;
        TransferTarget? activeTarget = null;
        TransferBadge? badge = null;
        var dragging = false;

        row.AddHandler(
            InputElement.PointerPressedEvent,
            OnPointerPressed,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        row.AddHandler(
            InputElement.PointerMovedEvent,
            OnPointerMoved,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        row.AddHandler(
            InputElement.PointerReleasedEvent,
            OnPointerReleased,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        row.PointerCaptureLost += (_, _) => Cancel();

        void OnPointerPressed(
            object? sender,
            PointerPressedEventArgs args)
        {
            if (!args.GetCurrentPoint(row).Properties.IsLeftButtonPressed
                || args.Source is Visual source
                && source.FindAncestorOfType<Button>() is not null
                || TransferMode(args.KeyModifiers) is not { } selectedMode)
            {
                return;
            }

            pressOrigin = args.GetPosition(row);
            pointer = args.Pointer;
            mode = selectedMode;
            dragging = false;
            pointer.Capture(row);
            args.Handled = true;
        }

        void OnPointerMoved(
            object? sender,
            PointerEventArgs args)
        {
            if (pointer is null
                || mode is null)
            {
                return;
            }
            if (!args.GetCurrentPoint(row).Properties.IsLeftButtonPressed)
            {
                Cancel();
                return;
            }

            if (!dragging)
            {
                var current = args.GetPosition(row);
                if (Math.Abs(current.X - pressOrigin.X) < 4
                    && Math.Abs(current.Y - pressOrigin.Y) < 4)
                {
                    return;
                }
                dragging = true;
                badge = new TransferBadge(row, node.Kind);
            }

            var target = TargetAt(args);
            SetActiveTarget(target);
            badge?.Show(
                args.GetPosition(row),
                mode.Value,
                target is not null);
            args.Handled = true;
        }

        async void OnPointerReleased(
            object? sender,
            PointerReleasedEventArgs args)
        {
            if (pointer is null
                || mode is not { } selectedMode)
            {
                return;
            }

            var target = dragging
                ? TargetAt(args) ?? activeTarget
                : null;
            Cancel();
            args.Handled = true;
            if (target is null)
            {
                return;
            }

            try
            {
                await transfer(
                    node,
                    target.Node,
                    selectedMode);
            }
            catch (Exception exception)
            {
                reportFailure(exception);
            }
        }

        TransferTarget? TargetAt(PointerEventArgs args)
        {
            if (TopLevel.GetTopLevel(row) is not { } topLevel)
            {
                return null;
            }
            var hit = topLevel.InputHitTest(
                args.GetPosition(topLevel));
            for (var visual = hit as Visual;
                 visual is not null;
                 visual = visual.GetVisualParent())
            {
                if (visual is Border candidate
                    && Targets.TryGetValue(candidate, out var target)
                    && ProductionHierarchyTransferContract.CanTransfer(
                        node,
                        target.Node))
                {
                    return target;
                }
            }
            return null;
        }

        void SetActiveTarget(TransferTarget? target)
        {
            if (ReferenceEquals(activeTarget, target))
            {
                return;
            }
            activeTarget?.Restore();
            activeTarget = target;
            activeTarget?.Activate();
        }

        void Cancel()
        {
            var capturedPointer = pointer;
            pointer = null;
            mode = null;
            dragging = false;
            SetActiveTarget(null);
            badge?.Hide();
            badge = null;
            capturedPointer?.Capture(null);
        }
    }

    internal static ProductionHierarchyTransferMode? TransferMode(
        KeyModifiers modifiers)
    {
        var command = modifiers.HasFlag(KeyModifiers.Meta);
        var option = modifiers.HasFlag(KeyModifiers.Alt);
        return (command, option) switch
        {
            (true, false) => ProductionHierarchyTransferMode.Copy,
            (false, true) => ProductionHierarchyTransferMode.Move,
            _ => null,
        };
    }

    private sealed class TransferTarget(
        Border row,
        ProjectTreeNode node,
        IBrush? originalBrush,
        Thickness originalThickness)
    {
        private readonly WeakReference<Border> _row = new(row);

        public ProjectTreeNode Node { get; } = node;

        public void Activate()
        {
            if (_row.TryGetTarget(out var current))
            {
                current.BorderBrush =
                    EditorAnimationVisuals.ActiveTrackBrush;
                current.BorderThickness = new Thickness(2);
            }
        }

        public void Restore()
        {
            if (_row.TryGetTarget(out var current))
            {
                current.BorderBrush = originalBrush;
                current.BorderThickness = originalThickness;
            }
        }
    }

    private sealed class TransferBadge
    {
        private static readonly IBrush ValidBrush =
            new SolidColorBrush(Color.Parse("#D6A638"));
        private static readonly IBrush InvalidBrush =
            new SolidColorBrush(Color.Parse("#E06C75"));
        private static readonly IBrush BackgroundBrush =
            new SolidColorBrush(Color.Parse("#F020252D"));

        private readonly ProjectTreeNodeKind _sourceKind;
        private readonly TextBlock _label;
        private readonly Border _content;
        private readonly Popup _popup;

        public TransferBadge(
            Border placementTarget,
            ProjectTreeNodeKind sourceKind)
        {
            _sourceKind = sourceKind;
            _label = new TextBlock
            {
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            };
            _content = new Border
            {
                Padding = new Thickness(9, 5),
                CornerRadius = new CornerRadius(10),
                BorderThickness = new Thickness(1),
                Background = BackgroundBrush,
                BoxShadow = new BoxShadows(
                    new BoxShadow
                    {
                        Blur = 10,
                        OffsetY = 3,
                        Color = Color.Parse("#66000000"),
                    }),
                IsHitTestVisible = false,
                Child = _label,
            };
            _popup = new Popup
            {
                PlacementTarget = placementTarget,
                Placement = PlacementMode.AnchorAndGravity,
                PlacementAnchor = PopupAnchor.TopLeft,
                PlacementGravity = PopupGravity.BottomRight,
                PlacementConstraintAdjustment =
                    PopupPositionerConstraintAdjustment.SlideX
                    | PopupPositionerConstraintAdjustment.SlideY,
                ShouldUseOverlayLayer = true,
                TakesFocusFromNativeControl = false,
                Child = _content,
            };
        }

        public void Show(
            Point position,
            ProductionHierarchyTransferMode mode,
            bool isValid)
        {
            var brush = isValid ? ValidBrush : InvalidBrush;
            _content.BorderBrush = brush;
            _label.Foreground = brush;
            _label.Text = isValid
                ? $"{ActionPrefix(mode)} {ActionName(mode)} {SourceName()}"
                : $"× {ActionName(mode)} {SourceName()} · destino no válido";
            _popup.PlacementRect = new Rect(
                position.X + 14,
                position.Y + 16,
                1,
                1);
            _popup.IsOpen = true;
        }

        public void Hide()
        {
            _popup.IsOpen = false;
        }

        private string SourceName() =>
            _sourceKind == ProjectTreeNodeKind.Shot
                ? "Shot"
                : "Screen";

        private static string ActionPrefix(
            ProductionHierarchyTransferMode mode) =>
            mode == ProductionHierarchyTransferMode.Copy
                ? "+"
                : "↕";

        private static string ActionName(
            ProductionHierarchyTransferMode mode) =>
            mode == ProductionHierarchyTransferMode.Copy
                ? "Copiar"
                : "Mover";
    }
}
